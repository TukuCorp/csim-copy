using Bunit;
using CarbonSim.Web.Components.Pages;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>One test per player screen: it renders the content the demo walk expects to see.</summary>
public sealed class PlayerScreensTests : ScreenTestContext
{
    public PlayerScreensTests()
    {
        View = PlayerViewFixture.Sample(SimulationId);
    }

    [Fact]
    public void Dashboard_shows_finance_compliance_position_and_history()
    {
        IRenderedComponent<Dashboard> page = RenderComponent<Dashboard>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("My finance");
        page.Markup.Should().Contain("My compliance");
        page.Markup.Should().Contain("My long / short position");
        page.Markup.Should().Contain(Localizer(Services)["MyAbatementImplementationStatus"].Value);
        page.Markup.Should().Contain("My auction history");
        page.Markup.Should().Contain("My trade history");
        page.Markup.Should().Contain("Delta Unit 1");
    }

    [Fact]
    public void Abatement_shows_the_cost_curve_undertaken_projects_and_opportunities()
    {
        IRenderedComponent<Abatement> page = RenderComponent<Abatement>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("Marginal abatement cost curve");
        page.Markup.Should().Contain(Localizer(Services)["AvailableEmissionReductionOpportunities"].Value);
        page.Markup.Should().Contain("Boiler efficiency");
        page.Markup.Should().Contain("Waste heat recovery");
        page.Markup.Should().Contain("Implement");
    }

    [Fact]
    public void Auction_shows_the_bid_form_the_lot_and_the_history()
    {
        IRenderedComponent<Auction> page = RenderComponent<Auction>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("Place bid");
        page.Markup.Should().Contain("Vintages this auction");
        page.Markup.Should().Contain(Localizer(Services)["AllowancesToBeAuctionedThisYear"].Value);
        page.Markup.Should().Contain("My auction history");
    }

    [Fact]
    public void Exchange_shows_the_price_chart_trade_activity_and_the_order_form()
    {
        IRenderedComponent<Exchange> page = RenderComponent<Exchange>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("Price history");
        page.Markup.Should().Contain("Trade activity");
        page.Markup.Should().Contain("Top of book");
        page.Markup.Should().Contain("Exchange market orders");
        page.Markup.Should().Contain("My current orders");
        Charts.Options.Should().ContainSingle(option => option.Contains("candlestick", StringComparison.Ordinal));
    }

    [Fact]
    public void Otc_shows_the_offer_form_and_both_offer_tables()
    {
        IRenderedComponent<Otc> page = RenderComponent<Otc>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("Send an offer to sell");
        page.Markup.Should().Contain("Offers waiting for my answer");
        page.Markup.Should().Contain("My offers");
        page.Markup.Should().Contain("Red River Power");
    }

    [Fact]
    public void Company_management_shows_the_books_and_the_holdings()
    {
        IRenderedComponent<CompanyManagement> page = RenderComponent<CompanyManagement>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("Company management");
        page.Markup.Should().Contain("Holdings");
        page.Markup.Should().Contain("Vintage 1");
        page.Markup.Should().Contain("Offsets");
    }

    [Fact]
    public void Unit_information_lists_the_units_and_their_position()
    {
        IRenderedComponent<UnitInformation> page = RenderComponent<UnitInformation>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("Unit information");
        page.Markup.Should().Contain("Delta Unit 1");
        page.Markup.Should().Contain("Automatic trading");
        page.Markup.Should().Contain("Baseline emissions");
    }

    [Fact]
    public void Surrender_and_banking_shows_the_position_and_the_holdings()
    {
        IRenderedComponent<SurrenderAndBanking> page = RenderComponent<SurrenderAndBanking>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("Surrender and banking");
        page.Markup.Should().Contain("Position summary");
        page.Markup.Should().Contain("Surrender status");
        page.Markup.Should().Contain("Banking limit");
    }

    [Fact]
    public void System_info_shows_the_parameters_the_report_and_the_reserve()
    {
        IRenderedComponent<SystemInfo> page = RenderComponent<SystemInfo>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("System info");
        page.Markup.Should().Contain("System report");
        page.Markup.Should().Contain("Government reserve");
        page.Markup.Should().Contain("Offset usage limit");
    }

    [Fact]
    public void Leaderboard_ranks_the_companies_and_marks_the_players_own()
    {
        IRenderedComponent<Leaderboard> page = RenderComponent<Leaderboard>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("Leaderboard");
        page.Markup.Should().Contain("Red River Power");
        page.Markup.Should().Contain("Marginal cost of compliance");
    }

    [Fact]
    public void Messages_shows_the_log_and_the_send_box()
    {
        IRenderedComponent<Messages> page = RenderComponent<Messages>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("Anyone selling vintage 1?");
        page.Markup.Should().Contain("Send");
    }

    [Fact]
    public void Waiting_lists_the_runs_and_their_settings()
    {
        Session.Runs = [PlayerViewFixture.Summary(SimulationId)];

        IRenderedComponent<Waiting> page = RenderComponent<Waiting>();

        page.Markup.Should().Contain("Waiting for the simulation to start");
        page.Markup.Should().Contain("Vietnam pilot");
        page.Markup.Should().Contain("Simulation settings");
        page.Markup.Should().Contain("There will be 3 years in the simulation");
    }

    [Fact]
    public void A_screen_without_a_company_points_at_signing_in()
    {
        View = null!;
        Session.View = null;

        IRenderedComponent<Dashboard> page = RenderComponent<Dashboard>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("has not claimed a company");
        page.Markup.Should().Contain("Sign in");
    }
}
