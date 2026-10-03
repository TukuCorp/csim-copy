using System.Security.Claims;
using CarbonSim.Data.Accounts;
using CarbonSim.Data.Persistence;
using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Reporting;
using CarbonSim.Web.Player;
using CarbonSim.Web.Simulations;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CarbonSim.Web.Admin;

/// <summary>
/// The console's read model and its front door. A screen asks for one <see cref="AdminSnapshot"/>,
/// which is built inside each run's gate from plain records, and every control is forwarded to the
/// driver, which is the only thing that touches the engine. The account's role is checked here on
/// every call, so a player who reaches a console screen gets a refusal rather than a run's data.
/// </summary>
public sealed class AdminSession : IAdminSession
{
    private readonly SimulationRegistry _runs;
    private readonly SimulationClockService _driver;
    private readonly IServiceScopeFactory _scopes;
    private readonly AuthenticationStateProvider _authentication;
    private readonly RegistrationPolicy _registration;
    private readonly CarbonSimHostOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AdminSession(
        SimulationRegistry runs,
        SimulationClockService driver,
        IServiceScopeFactory scopes,
        AuthenticationStateProvider authentication,
        RegistrationPolicy registration,
        CarbonSimHostOptions options,
        IHostEnvironment environment,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<bool> IsAdministratorAsync(CancellationToken cancellationToken = default)
    {
        ClaimsPrincipal user = await CurrentUserAsync().ConfigureAwait(false);

        return user.IsInRole(nameof(AccountRole.Administrator));
    }

    public async Task<AdminSnapshot> SnapshotAsync(Guid? runId, CancellationToken cancellationToken = default)
    {
        ClaimsPrincipal user = await CurrentUserAsync().ConfigureAwait(false);

        if (!user.IsInRole(nameof(AccountRole.Administrator)))
        {
            return AdminSnapshot.Denied(user.Identity?.Name);
        }

        List<AdminRunSummary> runs = [];

        foreach (Guid id in _runs.Ids)
        {
            if (_runs.Contains(id))
            {
                runs.Add(await _runs.ReadAsync(id, Summarise).ConfigureAwait(false));
            }
        }

        List<AdminSavedRun> saved = [];

        using (IServiceScope scope = _scopes.CreateScope())
        {
            ISimulationRepository store = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();

            foreach (SimulationSummary summary in await store.ListAsync(cancellationToken).ConfigureAwait(false))
            {
                saved.Add(new AdminSavedRun(
                    summary.Id,
                    summary.Name,
                    summary.State.ToString(),
                    summary.CurrentYear,
                    _runs.Contains(summary.Id)));
            }
        }

        Guid? selected = runId is { } candidate && _runs.Contains(candidate) ? candidate : null;
        AdminRunDetail? detail = selected is { } selectedId
            ? await _runs.ReadAsync(selectedId, BuildDetail).ConfigureAwait(false)
            : null;

        return new AdminSnapshot(
            true,
            user.Identity?.Name,
            null,
            null,
            runs,
            saved,
            selected,
            detail,
            new AdminRegistrationView(_registration.Open, _registration.Pin));
    }

    public async Task<ScenarioDraft?> SetupDraftAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsAdministratorAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        string path = Path.IsPathRooted(_options.ScenarioFile)
            ? _options.ScenarioFile
            : Path.Combine(_environment.ContentRootPath, _options.ScenarioFile);

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"The configured scenario '{path}' does not exist.");
        }

        ScenarioDraft draft = ScenarioDraft.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));

        // A run's identity comes from its seed, so a draft that seeded a run already under way
        // would build a second exercise over the first. The form opens on a seed that is free.
        HashSet<ulong> inUse = [.. _runs.Ids.Select(id => _runs.Run(id).Simulation.Seed)];

        while (draft.Seed is { } seed && inUse.Contains(seed))
        {
            draft.Seed = seed + 1;
        }

        return draft;
    }

    public async Task<Guid> CreateRunAsync(ScenarioDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        await RequireAdministratorAsync().ConfigureAwait(false);

        return await _driver.CreateRunAsync(draft.ToJson(), "administrator-setup", cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadRunAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.LoadRunAsync(simulationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRunAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.DeleteRunAsync(simulationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task BeginYearAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);

        SimulationState state = await _runs.ReadAsync(simulationId, run => run.Simulation.State).ConfigureAwait(false);

        switch (state)
        {
            case SimulationState.Pending:
                await _driver.StartAsync(simulationId, cancellationToken).ConfigureAwait(false);
                break;

            case SimulationState.YearEnded:
                await _driver.BeginNextYearAsync(simulationId, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new InvalidOperationException(
                    $"A simulation that is {state} cannot begin a year; pause, halt or end the year first.");
        }
    }

    public async Task PauseAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.PauseAsync(simulationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task ResumeAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.ResumeAsync(simulationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task HaltAsync(Guid simulationId, TimeSpan? warnLead, CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.RequestHaltAsync(simulationId, warnLead, cancellationToken).ConfigureAwait(false);
    }

    public async Task EndYearAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.EndYearAsync(simulationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task EndSimulationAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.EndSimulationAsync(simulationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetMessagingAsync(Guid simulationId, bool enabled, CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.SetMessagingAsync(simulationId, enabled, cancellationToken).ConfigureAwait(false);
    }

    public async Task IssueFineAsync(
        Guid simulationId,
        int unitId,
        decimal amount,
        string description,
        CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.IssueFineAsync(simulationId, unitId, amount, description, cancellationToken).ConfigureAwait(false);
    }

    public async Task DisburseOffsetsAsync(
        Guid simulationId,
        int companyId,
        decimal volume,
        CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.DisburseOffsetsAsync(simulationId, companyId, volume, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAbatementAsync(
        Guid simulationId,
        int unitId,
        int optionIndex,
        CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.AddAbatementAsync(simulationId, unitId, optionIndex, cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyShockAsync(
        Guid simulationId,
        int unitId,
        int year,
        decimal deltaTonnes,
        CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.ApplyShockAsync(simulationId, unitId, year, deltaTonnes, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAutoTradeAsync(
        Guid simulationId,
        int unitId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);
        await _driver.SetAutoTradeAsync(simulationId, unitId, enabled, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetRegistrationAsync(bool open, string pin, CancellationToken cancellationToken = default)
    {
        await RequireAdministratorAsync().ConfigureAwait(false);

        _registration.SetOpen(open);
        _registration.SetPin(pin);
    }

    public async Task<string?> CsvAsync(Guid simulationId, string report, CancellationToken cancellationToken = default)
    {
        if (!await IsAdministratorAsync(cancellationToken).ConfigureAwait(false)
            || !AdminReport.IsKnown(report)
            || !_runs.Contains(simulationId))
        {
            return null;
        }

        AdminRunDetail detail = await _runs.ReadAsync(simulationId, BuildDetail).ConfigureAwait(false);

        return AdminCsv.For(report, detail);
    }

    private async Task<ClaimsPrincipal> CurrentUserAsync()
    {
        // An HTTP request - a CSV export, say - has its own principal, and asking the circuit's
        // authentication provider outside a circuit throws. A live Blazor circuit is the reverse:
        // the request that opened it is long gone, so the provider is the only source.
        if (_httpContextAccessor?.HttpContext is { User.Identity.IsAuthenticated: true } context)
        {
            return context.User;
        }

        try
        {
            AuthenticationState state = await _authentication.GetAuthenticationStateAsync().ConfigureAwait(false);

            return state.User;
        }
        catch (InvalidOperationException)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }

    private async Task RequireAdministratorAsync()
    {
        if (!await IsAdministratorAsync().ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException("Only an administrator may drive a simulation.");
        }
    }

    private static AdminRunSummary Summarise(HostedRun run)
    {
        Simulation simulation = run.Simulation;
        Parameters parameters = simulation.TradingSystems.Single().Parameters;

        return new AdminRunSummary(
            simulation.Id,
            simulation.Name,
            simulation.State.ToString(),
            simulation.CurrentYear,
            parameters.Years,
            simulation.Companies.Count,
            simulation.Companies.Count(company => company.Owner.Kind == PlayerKind.Human),
            simulation.Units.Count,
            run.MessagingEnabled);
    }

    private static AdminRunDetail BuildDetail(HostedRun run)
    {
        Simulation simulation = run.Simulation;
        Parameters parameters = simulation.TradingSystems.Single().Parameters;
        int year = Math.Max(1, simulation.CurrentYear);
        CompliancePosition position = new(simulation);

        List<AdminSectorView> sectors =
        [
            .. simulation.Sectors.Select(sector => new AdminSectorView(
                sector.Name,
                sector.EmissionShare,
                simulation.Units.Count(unit => string.Equals(unit.Sector.Name, sector.Name, StringComparison.Ordinal)),
                simulation.Units
                    .Where(unit => string.Equals(unit.Sector.Name, sector.Name, StringComparison.Ordinal))
                    .Sum(unit => simulation.Allocation.FreeAllocationFor(unit, year)))),
        ];

        List<AdminCompanyView> companies = [];

        foreach (Company company in simulation.Companies)
        {
            CompanyScore score = Scoring.ForYear(simulation, company, year);
            CompanyCompliance? result = simulation.Compliance.Results
                .FirstOrDefault(candidate => ReferenceEquals(candidate.Company, company) && candidate.Year == year);
            decimal emissions = position.EmissionsFor(company, year);
            decimal free = simulation.Allocation.FreeAllocationFor(company, year);

            companies.Add(new AdminCompanyView(
                company.Id,
                company.Name,
                company.Sector.Name,
                company.Owner.Name,
                company.Owner.Kind == PlayerKind.Ai,
                company.Capital,
                emissions,
                result?.Obligation ?? emissions,
                free,
                result?.Shortfall ?? Math.Max(0m, emissions - free),
                score.CostOfCompliance,
                score.MarginalCostOfCompliance,
                Scoring.FinalPosition(simulation, company),
                result is not null,
                result?.PenaltyCash ?? 0m));
        }

        List<AdminUnitView> units = [];

        foreach (Unit unit in simulation.Units)
        {
            List<AdminAbatementOptionView> opportunities = [];

            for (int index = 0; index < unit.AbatementOptions.Count; index++)
            {
                AbatementOption option = unit.AbatementOptions[index];

                opportunities.Add(new AdminAbatementOptionView(
                    index,
                    option.Code,
                    option.Name,
                    option.AnnualReduction,
                    option.UpfrontCost,
                    option.ImplementationYears,
                    option.LifetimeYears,
                    option.AnnualNetRevenue,
                    simulation.Abatements.HasImplemented(unit, option)));
            }

            units.Add(new AdminUnitView(
                unit.Id,
                unit.Name,
                unit.Company.Name,
                unit.Sector.Name,
                unit.BaselineEmissions,
                simulation.Allocation.FreeAllocationFor(unit, year),
                position.EmissionsFor(unit, year),
                simulation.Abatements.ReductionsFor(unit, year),
                unit.AutoTrade,
                simulation.Abatements.IsShutDown(unit, year),
                simulation.Abatements.For(unit).Count,
                opportunities));
        }

        List<AdminLeaderView> leaderboard =
        [
            .. Leaderboard.Rank(simulation).Select(entry => new AdminLeaderView(
                entry.Rank,
                entry.Company.Name,
                string.Join(", ", entry.Company.Units.Select(unit => unit.Name)),
                entry.Company.Owner.Name,
                entry.OverallMarginalCostOfCompliance,
                entry.FinalPosition,
                entry.IsAutomated)),
        ];

        List<AdminPriceView> prices = [];

        foreach (int tradeYear in simulation.Allocation.Years)
        {
            List<MarketTrade> auctions =
            [
                .. simulation.Journal.In(tradeYear, TradeChannel.Auction).Where(trade => trade.Price > 0m),
            ];

            prices.Add(new AdminPriceView(
                tradeYear,
                simulation.Journal.Volume(tradeYear, tradeYear, kind: ProductKind.Allowance),
                simulation.Journal.AveragePrice(tradeYear, tradeYear, kind: ProductKind.Allowance),
                simulation.Journal.Volume(tradeYear, tradeYear, kind: ProductKind.Offset),
                simulation.Journal.AveragePrice(tradeYear, tradeYear, kind: ProductKind.Offset),
                auctions.Count == 0 ? null : auctions.Sum(trade => trade.Consideration) / auctions.Sum(trade => trade.Volume),
                auctions.Count == 0 ? null : auctions.Min(trade => trade.Price),
                auctions.Count == 0 ? null : auctions.Max(trade => trade.Price),
                simulation.Journal.In(tradeYear).Count()));
        }

        List<AdminSurrenderView> surrender = [];

        foreach (Company company in simulation.Companies)
        {
            CompanyCompliance? result = simulation.Compliance.Results
                .FirstOrDefault(candidate => ReferenceEquals(candidate.Company, company) && candidate.Year == year);
            decimal emissions = position.EmissionsFor(company, year);
            decimal free = simulation.Allocation.FreeAllocationFor(company, year);
            decimal shortfall = result?.Shortfall ?? Math.Max(0m, emissions - free);

            surrender.Add(new AdminSurrenderView(
                company.Id,
                company.Name,
                string.Join(", ", company.Units.Select(unit => unit.Name)),
                emissions,
                result?.Obligation ?? emissions,
                result?.Covered ?? (result is null ? Math.Min(emissions, free) : 0m),
                result?.AllowancesSurrendered ?? 0m,
                result?.OffsetsSurrendered ?? 0m,
                result?.Banked ?? 0m,
                result?.Forfeited ?? 0m,
                shortfall,
                result?.PenaltyCash ?? 0m,
                result is not null));
        }

        SystemReport report = SystemReport.For(simulation, year);

        List<HoldingView> reserve =
        [
            .. simulation.Allocation.Years.Select(vintage => new HoldingView(
                ProductView.Allowance(vintage),
                simulation.Government.Held(vintage),
                simulation.Government.Issued(vintage))),
        ];

        return new AdminRunDetail(
            simulation.Id,
            simulation.Name,
            simulation.State.ToString(),
            simulation.CurrentYear,
            parameters.Years,
            run.Clock.CurrentAuction,
            parameters.AuctionsPerYear,
            run.Clock.IsAuctionOpen,
            run.Clock.State == SimulationState.Running,
            run.MessagingEnabled,
            run.Clock.TimeLeftInYear,
            run.Clock.TimeToAuctionOpen,
            run.Clock.TimeToAuctionClose,
            ToParametersView(parameters),
            sectors,
            companies,
            units,
            leaderboard,
            prices,
            surrender,
            ToTotals(simulation, report.ThisYear, year, toDate: false),
            ToTotals(simulation, report.ToDate, year, toDate: true),
            simulation.Government.Revenue,
            reserve,
            simulation.State == SimulationState.YearEnded && simulation.CurrentYear >= parameters.Years);
    }

    private static AdminParametersView ToParametersView(Parameters parameters) => new(
        parameters.Cap,
        parameters.AnnualCapReductionRate,
        parameters.FreeAllocationShare,
        parameters.Years,
        parameters.OffsetUsageLimit,
        parameters.BankingLimit,
        parameters.PenaltyPerTonne,
        parameters.PenaltyAllowanceDebit,
        parameters.AuctionFloorPrice,
        parameters.AuctionCeilingPrice,
        parameters.AuctionsPerYear,
        parameters.YearLength,
        parameters.AuctionDuration,
        parameters.TradingOpenShareOfYear,
        parameters.VolatilityBand,
        parameters.OverdraftInterestRate,
        [.. parameters.BausGrowthBySector.Select(growth => new AdminGrowthView(growth.Sector, growth.MinAnnualRate, growth.MaxAnnualRate))]);

    private static SystemTotalsView ToTotals(Simulation simulation, SystemTotals totals, int year, bool toDate)
    {
        int through = toDate ? simulation.Allocation.Years.Max() : year;
        decimal cap = simulation.Allocation.Years
            .Where(candidate => candidate <= through)
            .Sum(simulation.Allocation.CapForYear);
        decimal free = simulation.Allocation.Years
            .Where(candidate => candidate <= through)
            .Sum(simulation.Allocation.FreeAllocationForYear);

        return new SystemTotalsView(
            totals.ForecastEmissions,
            totals.AllowancesSurrendered,
            totals.OffsetsSurrendered,
            totals.Banked,
            totals.Forfeited,
            totals.AllowancesSold,
            totals.AllowancesBought,
            totals.OffsetsSold,
            totals.OffsetsBought,
            totals.AuctionVolume,
            totals.AuctionRevenue,
            totals.AverageAllowancePrice,
            totals.AverageOffsetPrice,
            totals.AbatementsImplemented,
            totals.AbatementTonnes,
            totals.EmissionsReduced,
            totals.PenaltyCount,
            totals.PenaltyValue,
            totals.FineValue,
            cap,
            free,
            cap - free);
    }
}
