using System.Globalization;
using System.Security.Claims;
using CarbonSim.Data.Accounts;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Web.Accounts;
using CarbonSim.Web.Admin;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// The console's live views, its company reports and its yearly leaderboards are built from the run
/// itself: a bid placed in the open auction and an order resting on the exchange show up as the
/// trainer reads them.
/// </summary>
public sealed class ConsoleReportsTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public ConsoleReportsTests(SimulationTestHost host) => _host = host;

    [Fact]
    public async Task The_console_sees_the_open_auction_the_books_the_company_reports_and_the_year_boards()
    {
        string json = TestRunFactory.ScenarioJson.Replace("\"seed\": 7", "\"seed\": 8201", StringComparison.Ordinal);
        Guid id = Registry().Start(ScenarioLoader.LoadJson(json, "console-reports"));
        SimulationClockService driver = _host.CreateDriver();
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        await driver.StartAsync(id);
        Simulation simulation = Registry().Run(id).Simulation;
        decimal floor = simulation.Rules.AuctionFloorPrice;
        simulation.Ledger.Grant(simulation.FindUnit(1).Company, Product.Allowance(1), 1_000m);

        await Registry().BidAtAuctionAsync(id, delta.Actor, 1, 1, floor + 5m, 20_000m);
        await Registry().PlaceOrderAsync(id, delta.Actor, 1, "Vintage 1", "Sell", "Limit", 400m, floor + 9m);

        AdminRunDetail detail = (await Console().SnapshotAsync(id)).Run
            ?? throw new InvalidOperationException("no console run");

        AdminLiveLotView lot = detail.LiveAuction!.Lots.Single(candidate => candidate.Vintage == 1);
        lot.Bids.Should().Be(1);
        lot.Bidders.Should().Be(1);
        lot.BidVolume.Should().Be(20_000m);
        lot.HighestBid.Should().Be(floor + 5m);
        detail.LiveAuction.Open.Should().BeTrue();

        AdminLiveBookView book = detail.LiveBooks.Single(candidate => candidate.Product.Key == "Vintage 1");
        book.BestOffer.Should().Be(floor + 9m);
        book.AskVolume.Should().Be(400m);
        book.LastTrade.Should().BeNull("nothing has traded yet");

        detail.CompanyReports.Should().HaveCount(simulation.Companies.Count);
        detail.CompanyReports.Single(report => report.Company == TestRunFactory.DeltaCompany).Years.Should().ContainSingle()
            .Which.CapitalAtStart.Should().Be(simulation.FindUnit(1).Company.Capital,
                "nothing has been spent yet: setting the bid's money aside does not move capital");
        detail.LeaderboardYears.Should().ContainSingle().Which.Year.Should().Be(1);
    }

    private SimulationRegistry Registry() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    private AdminSession Console()
    {
        IServiceProvider services = _host.Server.Services;

        return new AdminSession(
            services.GetRequiredService<SimulationRegistry>(),
            _host.CreateDriver(),
            services.GetRequiredService<IServiceScopeFactory>(),
            new FixedAuthenticationStateProvider(),
            services.GetRequiredService<RegistrationPolicy>(),
            services.GetRequiredService<CarbonSimHostOptions>(),
            services.GetRequiredService<IHostEnvironment>());
    }

    private sealed class FixedAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, 1.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Role, nameof(AccountRole.Administrator)),
            ],
            authenticationType: "test"));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_user));
    }
}
