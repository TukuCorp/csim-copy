using System.Globalization;
using System.Security.Claims;
using Bunit;
using CarbonSim.Data.Accounts;
using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Web;
using CarbonSim.Web.Accounts;
using CarbonSim.Web.Admin;
using CarbonSim.Web.Player;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DashboardPage = CarbonSim.Web.Components.Pages.Dashboard;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// What the two front doors report: the console's reading of a run that has not begun or has run
/// out of time, and the waiting screen's answer to "who is signed in and where do they go next".
/// Both are built by the session a screen calls, against a real run.
/// </summary>
public sealed class ConsoleDoorTests : IClassFixture<SimulationTestHost>
{
    private const int YearMinutes = 20;

    private readonly SimulationTestHost _host;

    public ConsoleDoorTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task The_console_reads_a_year_that_has_not_begun_as_year_one_with_the_whole_year_ahead()
    {
        Guid id = StartRun(201);
        AdminSession session = Console(1);

        AdminSnapshot snapshot = await session.SnapshotAsync(id);
        AdminRunDetail? selected = snapshot.Run;
        selected.Should().NotBeNull();
        AdminRunDetail run = selected!;

        run.State.Should().Be(nameof(SimulationState.Pending));
        run.Year.Should().Be(1, "year one is what is waiting to start");
        run.Auction.Should().Be(1);
        run.AuctionsPerYear.Should().Be(4);
        run.TimeLeftInYear.Should().Be(TimeSpan.FromMinutes(YearMinutes));
        run.ClockRunning.Should().BeFalse();
        run.CanResume.Should().BeFalse("a year that has not begun is started, not resumed");
        snapshot.Runs.Should().ContainSingle(candidate => candidate.Id == id).Which.Year.Should().Be(1);
    }

    [Fact]
    public async Task The_waiting_screen_is_told_the_run_a_signed_in_player_holds_a_company_in()
    {
        Guid id = StartRun(203);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        PlayerSession session = Player(delta.AccountId);

        IReadOnlyList<RunSummary> runs = await session.RunsAsync();

        runs.Should().ContainSingle(candidate => candidate.Id == id).Which.Year
            .Should().Be(1, "a run waiting to start is about to play year one");

        FrontDoorView door = await session.FrontDoorAsync();

        door.IsAdministrator.Should().BeFalse();
        door.CompanyName.Should().Be(TestRunFactory.DeltaCompany);
        // The host may be holding several exercises built from the same scenario, all with the same
        // company names; what the front door promises is a run this company plays in, and it is
        // unambiguous in a real room where a company is claimed once.
        door.RunId.Should().NotBeNull("the player holds a company in a run on the shelf");
        runs.Should().Contain(candidate => candidate.Id == door.RunId);
        door.AccountName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_waiting_screen_sends_a_trainer_to_the_console_and_a_guest_to_sign_in()
    {
        PlayerAccount trainer = await _host.SeedAdministratorAsync("door-trainer@example.com");

        FrontDoorView console = await Player(trainer.Id).FrontDoorAsync();

        console.IsAdministrator.Should().BeTrue();
        console.RunId.Should().BeNull("a trainer's place is the console, not a company's screens");

        FrontDoorView guest = await Player(null).FrontDoorAsync();

        guest.AccountName.Should().BeNull();
        guest.IsAdministrator.Should().BeFalse();
        guest.RunId.Should().BeNull();
    }

    [Fact]
    public async Task The_console_counts_what_a_company_holds_against_what_it_owes()
    {
        Guid id = StartRun(204);
        SimulationClockService driver = _host.CreateDriver();
        HostedRun run = _host.Server.Services.GetRequiredService<SimulationRegistry>().Run(id);
        Company company = run.Simulation.FindCompany(1);
        AdminSession session = Console(1);

        await driver.StartAsync(id);

        AdminCompanyView before = Company(await session.SnapshotAsync(id), company.Id);

        before.Shortfall.Should().Be(
            Math.Max(0m, before.Emissions - before.FreeAllocation),
            "nothing has been bought or sold yet");

        Auction auction = run.Auctions.ForSection(1, 1);

        auction.PlaceBid(company.Units[0], 1, run.Simulation.Rules.AuctionFloorPrice, 20_000m);
        auction.Clear();

        decimal won = auction.Results
            .SelectMany(result => result.Awards)
            .Where(award => ReferenceEquals(award.Bid.Unit.Company, company))
            .Sum(award => award.Volume);

        won.Should().BePositive();

        AdminCompanyView after = Company(await session.SnapshotAsync(id), company.Id);
        Standing standing = new(run.Simulation);

        after.FinalPosition.Should().Be(before.FinalPosition - won, "the win counts against what the company owes");
        after.FinalPosition.Should().Be(-standing.Position(company, 1), "the leaderboard's position is short-positive");
    }

    [Fact]
    public async Task The_system_report_counts_the_cap_of_the_year_it_is_about_and_the_years_that_have_run()
    {
        Guid id = StartRun(205);
        SimulationClockService driver = _host.CreateDriver();
        HostedRun run = _host.Server.Services.GetRequiredService<SimulationRegistry>().Run(id);
        AllocationPlan allocation = run.Simulation.Allocation;
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);

        await driver.StartAsync(id);
        _host.FakeTime.Advance(TimeSpan.FromMinutes(YearMinutes));
        await driver.TickAsync(id);
        await driver.EndYearAsync(id);
        await driver.BeginNextYearAsync(id);

        AdminRunDetail? selected = (await Console(1).SnapshotAsync(id)).Run;

        selected.Should().NotBeNull();
        AdminRunDetail detail = selected!;

        // Year 2 is under way: this year is year 2 alone, to date is years 1 and 2.
        detail.ThisYear.Cap.Should().Be(allocation.CapForYear(2));
        detail.ThisYear.FreeAllocation.Should().Be(allocation.FreeAllocationForYear(2));
        detail.ToDate.Cap.Should().Be(allocation.CapForYear(1) + allocation.CapForYear(2));
        detail.ToDate.FreeAllocation.Should().Be(
            allocation.FreeAllocationForYear(1) + allocation.FreeAllocationForYear(2));

        PlayerView view = await Player(delta.AccountId).ForPlayerAsync(id) ?? throw new InvalidOperationException("no view");

        view.System.ThisYear.Cap.Should().Be(allocation.CapForYear(2), "the player reads the same report");
        view.System.ToDate.Cap.Should().Be(allocation.CapForYear(1) + allocation.CapForYear(2));
    }

    [Fact]
    public async Task Once_the_year_is_closed_the_player_reads_the_settlement_and_no_countdown()
    {
        Guid id = StartRun(206);
        SimulationClockService driver = _host.CreateDriver();
        HostedRun run = _host.Server.Services.GetRequiredService<SimulationRegistry>().Run(id);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);

        await driver.StartAsync(id);
        _host.FakeTime.Advance(TimeSpan.FromMinutes(YearMinutes));
        await driver.TickAsync(id);
        await driver.EndYearAsync(id);

        CompanyCompliance settled = run.Simulation.Compliance.Results
            .Single(result => result.Company.Id == 1 && result.Year == 1);
        PlayerView view = await Player(delta.AccountId).ForPlayerAsync(id) ?? throw new InvalidOperationException("no view");

        run.Simulation.State.Should().Be(SimulationState.YearEnded);
        view.Position.Settled.Should().BeTrue("the year has been reconciled");
        view.Position.Shortfall.Should().Be(settled.Shortfall, "the position is what the year end settled, not a fresh gap");
        view.Position.ForecastEmissions.Should().Be(settled.Obligation);
        view.Position.Held.Should().Be(settled.Banked, "everything else was surrendered or forfeited");
        view.YearIsOver.Should().BeTrue();
        view.TimeLeftInYear.Should().Be(TimeSpan.Zero, "a closed year has nothing left to count down");
        view.TimeToAuctionOpen.Should().Be(TimeSpan.Zero);
        view.TimeToAuctionClose.Should().Be(TimeSpan.Zero);

        AdminRunDetail? selected = (await Console(1).SnapshotAsync(id)).Run;

        selected.Should().NotBeNull();
        selected!.TimeLeftInYear.Should().Be(TimeSpan.Zero, "the console stops counting too");
        selected.TimeToAuctionOpen.Should().Be(TimeSpan.Zero);
        selected.TimeToAuctionClose.Should().Be(TimeSpan.Zero);
        selected.CountdownsApply.Should().BeFalse();
    }

    [Fact]
    public async Task The_book_is_read_by_price_level_with_every_order_at_a_price_added_up()
    {
        Guid id = StartRun(208);
        SimulationClockService driver = _host.CreateDriver();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SignedIn redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);

        await driver.StartAsync(id);

        // Three offers, two of them at the same price, and a bid below both.
        await runs.PlaceOrderAsync(id, delta.Actor, 1, "Vintage 1", "Sell", "Limit", 4_000m, 105m);
        await runs.PlaceOrderAsync(id, redRiver.Actor, 2, "Vintage 1", "Sell", "Limit", 6_000m, 105m);
        await runs.PlaceOrderAsync(id, delta.Actor, 1, "Vintage 1", "Sell", "Limit", 1_000m, 108m);
        await runs.PlaceOrderAsync(id, redRiver.Actor, 2, "Vintage 1", "Buy", "Limit", 2_000m, 95m);

        PlayerView view = await Player(delta.AccountId).ForPlayerAsync(id) ?? throw new InvalidOperationException("no view");
        ProductBookView book = view.Books.Single(candidate => candidate.Product.Key == "Vintage 1");

        book.AskLevels.Should().Equal(
            [new BookLevelView(105m, 10_000m), new BookLevelView(108m, 1_000m)],
            "the two offers at 105 are one level of ten thousand tonnes, best price first");
        book.BidLevels.Should().Equal([new BookLevelView(95m, 2_000m)]);
    }

    [Fact]
    public async Task The_players_view_survives_the_console_changing_the_run()
    {
        Guid id = StartRun(207);
        SimulationClockService driver = _host.CreateDriver();
        HostedRun run = _host.Server.Services.GetRequiredService<SimulationRegistry>().Run(id);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        Company company = run.Simulation.FindCompany(1);
        Unit unit = company.Units[0];

        await driver.StartAsync(id);

        PlayerView before = await Player(delta.AccountId).ForPlayerAsync(id) ?? throw new InvalidOperationException("no view");

        await driver.ApplyShockAsync(id, unit.Id, 1, 50_000m);
        await driver.DisburseOffsetsAsync(id, company.Id, 100_000m);
        await driver.AddAbatementAsync(id, unit.Id, 0);
        await driver.IssueFineAsync(id, unit.Id, 500_000m, "late report");

        PlayerView after = await Player(delta.AccountId).ForPlayerAsync(id) ?? throw new InvalidOperationException("no view");

        after.Position.ForecastEmissions.Should().Be(
            before.Position.ForecastEmissions + 50_000m,
            "the end-of-year modification raises what the unit is forecast to emit");
        after.Holdings.Should().Contain(holding => holding.Product.Key == "Offset" && holding.Held == 100_000m);
        after.Abatements.Should().ContainSingle();
        after.Finance.Capital.Should().Be(
            before.Finance.Capital - 500_000m - unit.AbatementOptions[0].UpfrontCost,
            "the fine and the abatement's capital both came out of the company");

        // The screens have to render it too: a view that reads is not a page that draws.
        using TestContext screens = new();

        screens.Services.AddLocalization();
        screens.Services.AddSingleton<ICharts>(new CarbonSim.Web.Tests.Components.RecordingCharts());
        screens.JSInterop.Mode = Bunit.JSRuntimeMode.Loose;

        IRenderedComponent<DashboardPage> page = screens.RenderComponent<DashboardPage>(parameters => parameters
            .AddCascadingValue(new PlayerScreenSnapshot(id, after, null, null))
            .Add(component => component.SimulationId, id));

        page.Markup.Should().Contain("Forecast emissions");
    }

    private static AdminCompanyView Company(AdminSnapshot snapshot, int companyId)
    {
        AdminRunDetail? detail = snapshot.Run;

        detail.Should().NotBeNull();

        return detail!.Companies.Single(candidate => candidate.Id == companyId);
    }

    private AdminSession Console(int accountId)
    {
        IServiceProvider services = _host.Server.Services;

        return new AdminSession(
            services.GetRequiredService<SimulationRegistry>(),
            _host.CreateDriver(),
            services.GetRequiredService<IServiceScopeFactory>(),
            new FixedAuthenticationStateProvider(accountId, AccountRole.Administrator),
            services.GetRequiredService<RegistrationPolicy>(),
            services.GetRequiredService<CarbonSimHostOptions>(),
            services.GetRequiredService<IHostEnvironment>());
    }

    private PlayerSession Player(int? accountId)
    {
        IServiceProvider services = _host.Server.Services;

        return new PlayerSession(
            services.GetRequiredService<SimulationRegistry>(),
            services.GetRequiredService<IServiceScopeFactory>(),
            new FixedAuthenticationStateProvider(accountId, AccountRole.Player));
    }

    private Guid StartRun(int seed)
    {
        string json = TestRunFactory.ScenarioJson.Replace("\"seed\": 7", $"\"seed\": {seed}", StringComparison.Ordinal);

        return _host.Server.Services
            .GetRequiredService<SimulationRegistry>()
            .Start(ScenarioLoader.LoadJson(json, "console-door-test"));
    }

    /// <summary>Signs a session in without a Blazor circuit: the account id a real cookie carries.</summary>
    private sealed class FixedAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;

        public FixedAuthenticationStateProvider(int? accountId, AccountRole role)
        {
            List<Claim> claims = [];

            if (accountId is { } id)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, id.ToString(CultureInfo.InvariantCulture)));
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            }

            _user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_user));
    }
}
