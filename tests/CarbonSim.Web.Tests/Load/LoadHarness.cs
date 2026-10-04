using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Claims;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Web.Contracts;
using CarbonSim.Web.Player;
using CarbonSim.Web.Simulations;
using CarbonSim.Web.Tests.Simulations;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Load;

/// <summary>How one load run should be set up and driven.</summary>
public sealed record LoadOptions(
    int Clients,
    int YearLengthMinutes,
    int Years,
    bool HandDriven,
    TimeSpan RefreshInterval,
    TimeSpan TickStep,
    TimeSpan MonitorInterval,
    double ActingShare,
    int Seed)
{
    /// <summary>The production shape: 250 screens refreshing once a second through a 20-minute year.</summary>
    public static LoadOptions Full { get; } = new(
        Clients: 250,
        YearLengthMinutes: 20,
        Years: 1,
        HandDriven: false,
        RefreshInterval: TimeSpan.FromSeconds(1),
        TickStep: TimeSpan.Zero,
        MonitorInterval: TimeSpan.FromMilliseconds(200),
        ActingShare: 0.25,
        Seed: 20261004);

    /// <summary>The same shape compressed hard, so the harness is guarded by the normal test run.</summary>
    public static LoadOptions Smoke { get; } = new(
        Clients: 12,
        YearLengthMinutes: 1,
        Years: 1,
        HandDriven: true,
        RefreshInterval: TimeSpan.FromMilliseconds(20),
        TickStep: TimeSpan.FromSeconds(1),
        MonitorInterval: TimeSpan.Zero,
        ActingShare: 0.25,
        Seed: 20261004);
}

/// <summary>One auction: when it was due to close, and when it was actually cleared.</summary>
public sealed record AuctionTiming(int Year, int Sequence, TimeSpan ScheduledClose, TimeSpan ClearedAt)
{
    public TimeSpan Lateness => ClearedAt > ScheduledClose ? ClearedAt - ScheduledClose : TimeSpan.Zero;
}

/// <summary>What a load run measured.</summary>
public sealed record LoadMetrics(
    int Clients,
    int Years,
    TimeSpan YearLength,
    int Refreshes,
    int Actions,
    int RefusedActions,
    int RefreshErrors,
    IReadOnlyList<AuctionTiming> Auctions,
    int MissedAuctions,
    TimeSpan RefreshP50,
    TimeSpan RefreshP95,
    TimeSpan RefreshP99,
    TimeSpan RefreshMax,
    TimeSpan GateWaitP50,
    TimeSpan GateWaitP95,
    TimeSpan GateWaitP99,
    TimeSpan GateWaitMax,
    TimeSpan CpuTime,
    long PeakWorkingSetBytes,
    TimeSpan WallTime)
{
    public TimeSpan AuctionLatenessMax =>
        Auctions.Count == 0 ? TimeSpan.Zero : Auctions.Max(auction => auction.Lateness);

    /// <summary>A one-line summary for the review and the harness's own log.</summary>
    public string Describe() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"clients={Clients} years={Years} year={YearLength.TotalMinutes:0.##}min refreshes={Refreshes} "
            + $"actions={Actions} (refused {RefusedActions}, errors {RefreshErrors}) "
            + $"auctions={Auctions.Count} missed={MissedAuctions} maxLateness={AuctionLatenessMax.TotalMilliseconds:0}ms "
            + $"refresh p50={RefreshP50.TotalMilliseconds:0.##}ms p95={RefreshP95.TotalMilliseconds:0.##}ms "
            + $"p99={RefreshP99.TotalMilliseconds:0.##}ms max={RefreshMax.TotalMilliseconds:0.##}ms "
            + $"gate p50={GateWaitP50.TotalMilliseconds:0.##}ms p95={GateWaitP95.TotalMilliseconds:0.##}ms "
            + $"p99={GateWaitP99.TotalMilliseconds:0.##}ms max={GateWaitMax.TotalMilliseconds:0.##}ms "
            + $"cpu={CpuTime.TotalSeconds:0.##}s peakWorkingSet={PeakWorkingSetBytes / (1024 * 1024)}MB "
            + $"wall={WallTime.TotalSeconds:0.##}s");
}

/// <summary>
/// The Phase 6 load harness. It boots the real host, signs in the requested number of players
/// through the real registration and sign-in endpoints, starts the load scenario, then keeps every
/// player's view refreshing at the production interval while a realistic share of them bid into
/// auctions and place exchange orders. It watches each auction's scheduled close, measures the
/// gate contention and the refresh latency, and samples the process's CPU and memory, so the load
/// test's numbers come from the production read path rather than a stand-in for it.
/// </summary>
internal sealed class LoadHarness
{
    private const string Password = "load-test-2026";

    private readonly LoadOptions _options;
    private readonly Action<string>? _log;
    private readonly CancellationToken _token;
    private TimeSpan _virtualElapsed;

    private LoadHarness(LoadOptions options, Action<string>? log, CancellationToken token)
    {
        _options = options;
        _log = log;
        _token = token;
    }

    public static Task<LoadMetrics> RunAsync(LoadOptions options, Action<string>? log = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new LoadHarness(options, log, cancellationToken).ExecuteAsync();
    }

    private async Task<LoadMetrics> ExecuteAsync()
    {
        string scenario = LoadScenario.Build(_options.Clients, _options.YearLengthMinutes, _options.Years);
        using LoadTestHost host = new(scenario, _options.HandDriven);

        SimulationRegistry runs = host.Server.Services.GetRequiredService<SimulationRegistry>();
        SimulationClockService driver = host.Server.Services.GetRequiredService<SimulationClockService>();

        _log?.Invoke($"signing in {_options.Clients} players...");
        IReadOnlyList<(int AccountId, string Company)> accounts = await SignInAllAsync(host).ConfigureAwait(false);
        _log?.Invoke($"signed in {accounts.Count} players; starting the run...");

        Guid runId = await driver.CreateRunAsync(scenario, "load-run").ConfigureAwait(false);
        HostedRun run = runs.Run(runId);
        List<LoadSession> sessions = BuildSessions(host, runs, run, accounts);

        Process process = Process.GetCurrentProcess();
        TimeSpan cpuBefore = process.TotalProcessorTime;
        long peakWorkingSet = process.WorkingSet64;

        ConcurrentQueue<TimeSpan> refreshes = new();
        ConcurrentQueue<TimeSpan> gateWaits = new();
        List<AuctionTiming> auctions = [];
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(_token);

        Stopwatch clock = new();
        _virtualElapsed = TimeSpan.Zero;

        await driver.StartAsync(runId).ConfigureAwait(false);
        clock.Start();

        List<Task> work = [.. sessions.Select(session => RefreshLoopAsync(session, refreshes, stop.Token))];
        work.Add(GateSamplerLoopAsync(runs, runId, gateWaits, stop.Token));
        work.Add(_options.HandDriven
            ? DriveHandClockAsync(run, driver, runId, auctions, host.FakeTime!, stop.Token)
            : ObserveRealClockAsync(run, driver, runId, auctions, clock, process, value => peakWorkingSet = Math.Max(peakWorkingSet, value), stop.Token));

        try
        {
            await work[^1].ConfigureAwait(false);
        }
        finally
        {
            stop.Cancel();

            try
            {
                await Task.WhenAll(work).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected: every loop ends by cancellation once the year is done.
            }
        }

        clock.Stop();
        TimeSpan cpuAfter = process.TotalProcessorTime;
        int missed = CountMissed(run, auctions, clock.Elapsed);

        return Summarise(sessions, refreshes, gateWaits, auctions, missed, cpuAfter - cpuBefore, peakWorkingSet, clock.Elapsed);
    }

    /// <summary>Signs in every player through the real endpoints and returns their account ids.</summary>
    private async Task<IReadOnlyList<(int AccountId, string Company)>> SignInAllAsync(LoadTestHost host)
    {
        ConcurrentBag<(int AccountId, string Company)> found = [];

        await Parallel.ForEachAsync(
            Enumerable.Range(1, _options.Clients),
            new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = _token },
            async (index, cancellation) =>
            {
                string company = $"Load Co {index:D3}";
                string email = $"load-{index:D4}@example.com";

                using HttpClient client = host.CreateClient();
                using HttpResponseMessage registered = await client.RegisterAsync(email, company, host.RegistrationPin, Password).ConfigureAwait(false);

                if (!registered.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Registering '{company}' failed: {(int)registered.StatusCode} {await registered.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false)}");
                }

                using HttpResponseMessage loggedIn = await client.LoginAsync(email, Password).ConfigureAwait(false);
                AccountResponse account = await loggedIn.Content.ReadFromJsonAsync<AccountResponse>(cancellation).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Signing in '{email}' returned no account.");

                found.Add((account.Id, company));
            }).ConfigureAwait(false);

        return [.. found.OrderBy(entry => entry.Company, StringComparer.Ordinal)];
    }

    private List<LoadSession> BuildSessions(
        LoadTestHost host,
        SimulationRegistry runs,
        HostedRun run,
        IReadOnlyList<(int AccountId, string Company)> accounts)
    {
        IServiceScopeFactory scopes = host.Server.Services.GetRequiredService<IServiceScopeFactory>();
        Guid runId = run.Simulation.Id;
        List<LoadSession> sessions = [];

        foreach ((int accountId, string companyName) in accounts)
        {
            Company company = run.Simulation.Companies.First(candidate => string.Equals(candidate.Name, companyName, StringComparison.Ordinal));
            PlayerSession session = new(runs, scopes, new StubAuthenticationStateProvider(accountId));
            bool acting = new Random(unchecked((accountId * 31) ^ _options.Seed)).NextDouble() < _options.ActingShare;

            sessions.Add(new LoadSession(session, accountId, companyName, company.Units[0].Id, runId, acting, _options.Seed));
        }

        return sessions;
    }

    private async Task RefreshLoopAsync(LoadSession session, ConcurrentQueue<TimeSpan> refreshes, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            long start = Stopwatch.GetTimestamp();

            try
            {
                PlayerView? view = await session.Session.ForPlayerAsync(session.RunId, token).ConfigureAwait(false);

                if (view is null)
                {
                    session.Errors++;
                }
                else
                {
                    refreshes.Enqueue(Stopwatch.GetElapsedTime(start));
                    session.Seen++;
                    await MaybeActAsync(session, view, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                session.Errors++;
            }

            try
            {
                await Task.Delay(_options.RefreshInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>A deterministic share of the room trades, at a rate that keeps the load real but bounded.</summary>
    private async Task MaybeActAsync(LoadSession session, PlayerView view, CancellationToken token)
    {
        if (!session.Acting || session.Actions >= 24)
        {
            return;
        }

        session.SeenSinceAct++;

        if (session.SeenSinceAct < 8)
        {
            return;
        }

        session.SeenSinceAct = 0;
        token.ThrowIfCancellationRequested();

        try
        {
            if (view.AuctionOpen)
            {
                decimal price = view.Books[0].BandFloor + session.Rng.Next(0, 40);
                await session.Session.PlaceBidAsync(session.RunId, session.UnitId, vintage: 1, price, volume: 1_000m, token).ConfigureAwait(false);
            }
            else
            {
                bool sell = session.Rng.Next(2) == 0;
                await session.Session.PlaceOrderAsync(
                    session.RunId,
                    session.UnitId,
                    product: "Vintage 1",
                    side: sell ? "Sell" : "Buy",
                    kind: "Limit",
                    volume: 500m,
                    price: view.Books[0].BandFloor + session.Rng.Next(-10, 10),
                    stopPrice: null,
                    fillPolicy: "AllowPartial",
                    token).ConfigureAwait(false);
            }

            session.Actions++;
        }
        catch (Exception)
        {
            session.RefusedActions++;
        }
    }

    /// <summary>Times a minimal read, which is the run's gate and nothing else, to see the contention.</summary>
    private async Task GateSamplerLoopAsync(SimulationRegistry runs, Guid runId, ConcurrentQueue<TimeSpan> gateWaits, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            long start = Stopwatch.GetTimestamp();

            try
            {
                await runs.ReadAsync(runId, _ => 0).ConfigureAwait(false);
                gateWaits.Enqueue(Stopwatch.GetElapsedTime(start));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // A run that went away; the loop ends with the harness.
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>The hand-driven clock: advance the fake time in small steps and tick the driver.</summary>
    private async Task DriveHandClockAsync(
        HostedRun run,
        SimulationClockService driver,
        Guid runId,
        List<AuctionTiming> auctions,
        FakeTimeProvider fake,
        CancellationToken token)
    {
        int yearsClosed = 0;

        while (yearsClosed < _options.Years)
        {
            token.ThrowIfCancellationRequested();
            fake.Advance(_options.TickStep);
            _virtualElapsed += _options.TickStep;
            await driver.TickAsync(runId, token).ConfigureAwait(false);
            RecordCleared(run, _virtualElapsed, auctions);

            // Pace the virtual clock against the wall clock so the refresh loops actually interleave
            // with the ticks; without it the whole year finishes before a screen has refreshed once.
            await Task.Delay(TimeSpan.FromMilliseconds(5), token).ConfigureAwait(false);

            if (run.Clock.State == SimulationState.TradingHalted)
            {
                await driver.EndYearAsync(runId, token).ConfigureAwait(false);
                yearsClosed++;

                if (yearsClosed < _options.Years)
                {
                    await driver.BeginNextYearAsync(runId, token).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>The real clock runs itself; this watches for the year end and records the auctions.</summary>
    private async Task ObserveRealClockAsync(
        HostedRun run,
        SimulationClockService driver,
        Guid runId,
        List<AuctionTiming> auctions,
        Stopwatch clock,
        Process process,
        Action<long> sample,
        CancellationToken token)
    {
        int yearsClosed = 0;
        TimeSpan ceiling = TimeSpan.FromMinutes(_options.YearLengthMinutes * (double)_options.Years) + TimeSpan.FromSeconds(90);

        while (yearsClosed < _options.Years)
        {
            token.ThrowIfCancellationRequested();
            sample(process.WorkingSet64);
            RecordCleared(run, clock.Elapsed, auctions);

            if (run.Clock.State == SimulationState.TradingHalted)
            {
                await driver.EndYearAsync(runId, token).ConfigureAwait(false);
                yearsClosed++;

                if (yearsClosed < _options.Years)
                {
                    await driver.BeginNextYearAsync(runId, token).ConfigureAwait(false);
                }
            }
            else if (clock.Elapsed > ceiling)
            {
                throw new InvalidOperationException(
                    $"The year did not finish within {ceiling}: the run is {run.Clock.State} at {clock.Elapsed}.");
            }

            await Task.Delay(_options.MonitorInterval, token).ConfigureAwait(false);
        }
    }

    private static void RecordCleared(HostedRun run, TimeSpan elapsed, List<AuctionTiming> auctions)
    {
        foreach (Auction auction in run.Auctions.Auctions)
        {
            if (!auction.IsCleared)
            {
                continue;
            }

            TimeSpan scheduled = ScheduledClose(run, auction);

            if (scheduled > elapsed || auctions.Any(recorded => recorded.Year == auction.Year && recorded.Sequence == auction.Sequence))
            {
                continue;
            }

            auctions.Add(new AuctionTiming(auction.Year, auction.Sequence, scheduled, elapsed));
        }
    }

    private static TimeSpan ScheduledClose(HostedRun run, Auction auction)
    {
        Parameters parameters = run.Simulation.TradingSystems[0].Parameters;
        double section = parameters.YearLength.TotalMinutes / parameters.AuctionsPerYear;

        return TimeSpan.FromMinutes(((auction.Year - 1) * parameters.YearLength.TotalMinutes) + (auction.Sequence * section));
    }

    private int CountMissed(HostedRun run, List<AuctionTiming> auctions, TimeSpan elapsed)
    {
        int missed = 0;

        foreach (Auction auction in run.Auctions.Auctions)
        {
            TimeSpan scheduled = ScheduledClose(run, auction);

            if (scheduled <= elapsed && !auctions.Any(recorded => recorded.Year == auction.Year && recorded.Sequence == auction.Sequence))
            {
                missed++;
            }
        }

        return missed;
    }

    private LoadMetrics Summarise(
        List<LoadSession> sessions,
        ConcurrentQueue<TimeSpan> refreshes,
        ConcurrentQueue<TimeSpan> gateWaits,
        List<AuctionTiming> auctions,
        int missed,
        TimeSpan cpu,
        long peakWorkingSet,
        TimeSpan wall)
    {
        TimeSpan[] refreshSamples = [.. refreshes.Order()];
        TimeSpan[] gateSamples = [.. gateWaits.Order()];

        return new LoadMetrics(
            _options.Clients,
            _options.Years,
            TimeSpan.FromMinutes(_options.YearLengthMinutes),
            refreshSamples.Length,
            sessions.Sum(session => session.Actions),
            sessions.Sum(session => session.RefusedActions),
            sessions.Sum(session => session.Errors),
            auctions,
            missed,
            Percentile(refreshSamples, 0.50),
            Percentile(refreshSamples, 0.95),
            Percentile(refreshSamples, 0.99),
            refreshSamples.Length == 0 ? TimeSpan.Zero : refreshSamples[^1],
            Percentile(gateSamples, 0.50),
            Percentile(gateSamples, 0.95),
            Percentile(gateSamples, 0.99),
            gateSamples.Length == 0 ? TimeSpan.Zero : gateSamples[^1],
            cpu,
            peakWorkingSet,
            wall);
    }

    private static TimeSpan Percentile(IReadOnlyList<TimeSpan> sorted, double percentile)
    {
        if (sorted.Count == 0)
        {
            return TimeSpan.Zero;
        }

        int index = (int)Math.Ceiling(percentile * sorted.Count) - 1;

        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    /// <summary>One signed-in player's refresh loop and what it has done.</summary>
    private sealed class LoadSession
    {
        public LoadSession(PlayerSession session, int accountId, string company, int unitId, Guid runId, bool acting, int seed)
        {
            Session = session;
            AccountId = accountId;
            Company = company;
            UnitId = unitId;
            RunId = runId;
            Acting = acting;
            Rng = new Random(unchecked((accountId * 397) ^ seed));
        }

        public PlayerSession Session { get; }

        public int AccountId { get; }

        public string Company { get; }

        public int UnitId { get; }

        public Guid RunId { get; }

        public bool Acting { get; }

        public Random Rng { get; }

        public int Seen { get; set; }

        public int SeenSinceAct { get; set; }

        public int Actions { get; set; }

        public int RefusedActions { get; set; }

        public int Errors { get; set; }
    }

    /// <summary>Presents the account id a signed-in cookie would carry, without a Blazor circuit.</summary>
    private sealed class StubAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;

        public StubAuthenticationStateProvider(int accountId)
        {
            _user = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, accountId.ToString(CultureInfo.InvariantCulture))],
                authenticationType: "load"));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_user));
    }
}
