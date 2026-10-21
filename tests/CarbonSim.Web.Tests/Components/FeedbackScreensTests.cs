using System.Text.Json;
using Bunit;
using CarbonSim.Web.Components.Layout;
using CarbonSim.Web.Components.Pages;
using CarbonSim.Web.Components.Shared;
using CarbonSim.Web.Player;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The screens after QA's second pass: what the exchange strip and the top of the book say, that an
/// auction result stays on screen with what the player won and paid, that the cost curve is drawn
/// to scale, that a list names a unit once, and that the shell reports what a control did.
/// </summary>
public sealed class FeedbackScreensTests : ScreenTestContext
{
    public FeedbackScreensTests()
    {
        View = PlayerViewFixture.Sample(SimulationId);
    }

    [Fact]
    public void System_info_shows_the_two_market_settings_a_trainer_can_set()
    {
        IRenderedComponent<SystemInfo> page = RenderComponent<SystemInfo>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain(
            Localizer(Services)["GovernmentReserveToAuctionPercent"].Value,
            "the reserve re-offer share is a run setting and belongs on the system screen");
        page.Markup.Should().Contain(
            Localizer(Services)["ForwardVintageShare"].Value,
            "so is the forward vintage share, which the host plays the run with");
    }

    [Fact]
    public void The_exchange_summary_groups_the_auction_the_exchange_and_the_otc_market()
    {
        IRenderedComponent<Exchange> page = RenderComponent<Exchange>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));
        string floor = Localizer(Services)["AuctionFloorPrice"].Value;

        page.Markup.Should().Contain($"<h3>{Localizer(Services)["AllowanceAuction"].Value}</h3>");
        page.Markup.Should().Contain($"<h3>{Localizer(Services)["ExchangeMarket"].Value}</h3>");
        page.Markup.Should().Contain($"<h3>{Localizer(Services)["OtcMarket"].Value}</h3>");
        page.Markup.Should().Contain($">{floor}</dt>", "the floor price stands on its own now");
        page.Markup.Should().NotContain(
            $">{Localizer(Services)["AllowanceAuction"].Value} {floor}<",
            "the strip used to read Allowance auction Auction floor price");
        page.Markup.Should().Contain(
            $">{Localizer(Services)["OtcBestPrice"].Value}</dt><dd>{Display.Price(110m)}</dd>");
    }

    [Fact]
    public void The_top_of_book_pairs_bids_against_offers_under_buying_and_selling_headers()
    {
        IRenderedComponent<Exchange> page = RenderComponent<Exchange>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain($"<th>{Localizer(Services)["Buying"].Value}</th>");
        page.Markup.Should().Contain($"<th>{Localizer(Services)["Selling"].Value}</th>");
        // A price level carries every order at that price added together, and the asks are on the
        // selling side of the same row: the book is read by price, not order by order.
        page.Markup.Should().Contain($"5,000 @ {Display.Price(108m)}");
        page.Markup.Should().Contain($"5,000 @ {Display.Price(112m)}");
    }

    [Fact]
    public void An_auction_result_stays_on_screen_with_what_the_player_won_and_paid()
    {
        IRenderedComponent<Auction> page = RenderComponent<Auction>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));
        string won = Localizer(Services)["MyWon"].Value;
        string paid = Localizer(Services)["MyPaid"].Value;

        // The cleared auction is section 1 even though the clock is on section 2.
        page.Markup.Should().Contain(Localizer(Services)["AuctionHasEnded"].Value);
        page.Markup.Should().Contain($"<dt>{won}</dt><dd>20,000</dd>");
        page.Markup.Should().Contain($"<dt>{paid}</dt><dd>{Display.Money(2_000_000m)}</dd>");

        List<string> headers = [.. page.FindAll("th").Select(header => header.TextContent.Trim())];

        headers.Should().Contain(won, "the history table carries my own columns");
        headers.Should().Contain(paid);
        headers.Should().Contain(Localizer(Services)["TotalAwarded"].Value, "the system-wide column is named as such");
    }

    [Fact]
    public void The_cost_curve_sizes_each_bar_by_the_tonnes_it_abates_and_marks_my_shortfall()
    {
        List<AbatementOpportunityView> options =
        [
            new(1, "Delta Unit 1", 0, "P1", "Boiler efficiency", 100_000m, 10_000m, 1, 10, 0m, 0m, 50m, false),
            new(1, "Delta Unit 1", 1, "P2", "Waste heat recovery", 100_000m, 100_000m, 1, 10, 0m, 0m, 20m, false),
        ];

        // Fully named: this test class holds a RecordingCharts in a property called Charts.
        using JsonDocument option = JsonDocument.Parse(
            CarbonSim.Web.Player.Charts.Macc(options, 55_000m, "Shortfall"));
        JsonElement series = option.RootElement.GetProperty("series");

        // Cheapest first, each bar as wide as its own tonnes (the widest is 120px).
        series[0].GetProperty("name").GetString().Should().Contain("P2").And.Contain("100,000 t");
        series[0].GetProperty("barWidth").GetInt32().Should().Be(120);
        series[1].GetProperty("name").GetString().Should().Contain("P1").And.Contain("10,000 t");
        series[1].GetProperty("barWidth").GetInt32().Should().Be(12);

        // The gap the company still has to cover is drawn across the curve.
        JsonElement marker = series[series.GetArrayLength() - 1]
            .GetProperty("markLine")
            .GetProperty("data")[0];

        marker.GetProperty("xAxis").GetDecimal().Should().Be(55_000m);
        option.RootElement.GetProperty("xAxis").GetProperty("name").GetString().Should().Be("tonnes");
        option.RootElement.GetProperty("xAxis").GetProperty("max").GetDecimal().Should().Be(110_000m);
    }

    [Fact]
    public void The_leaderboard_names_a_unit_once_and_reads_the_final_position_as_long_or_short()
    {
        View = View with
        {
            Leaderboard =
            [
                .. View.Leaderboard,
                // A scenario names a one-unit company after itself: "name (company)" would repeat it.
                new LeaderboardRow(3, "AI Cement 01", "AI Cement 01", "AI Cement 01", 0m, 0m, 0m, true, false),
            ],
        };

        IRenderedComponent<Leaderboard> page = RenderComponent<Leaderboard>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("AI Cement 01 (AI)");
        page.Markup.Should().NotContain("AI Cement 01 (AI Cement 01)");
        page.Markup.Should().Contain("Short 81,000", "a company holding less than it owes is short");
        page.Markup.Should().Contain("Long 1,000", "a company holding more than it owes is long");
    }

    [Fact]
    public void The_price_and_cost_curves_keep_room_at_the_top_for_a_wrapped_legend()
    {
        // A phone wraps the legend onto a second line; the plot has to start below it or the
        // legend lands on the axis title and the top gridline.
        using JsonDocument prices = JsonDocument.Parse(
            CarbonSim.Web.Player.Charts.Prices([], 3, 100m, "Auction floor price", year => $"Year {year}"));
        JsonElement priceGrid = prices.RootElement.GetProperty("grid");
        JsonElement priceLegend = prices.RootElement.GetProperty("legend");

        priceGrid.GetProperty("top").GetInt32().Should().BeGreaterThanOrEqualTo(52);
        priceLegend.GetProperty("top").GetInt32().Should().Be(0);
        priceLegend.GetProperty("type").GetString().Should().Be("scroll", "a narrow legend scrolls rather than covers the plot");
        prices.RootElement.GetProperty("yAxis").GetProperty("name").GetString().Should().Be("per tonne");

        using JsonDocument macc = JsonDocument.Parse(
            CarbonSim.Web.Player.Charts.Macc([], 0m, "Shortfall"));

        macc.RootElement.GetProperty("grid").GetProperty("top").GetInt32().Should().BeGreaterThanOrEqualTo(44);
        macc.RootElement.GetProperty("xAxis").GetProperty("name").GetString().Should().Be("tonnes");
    }

    [Fact]
    public void The_price_chart_draws_the_year_month_by_month()
    {
        // The original's exchange chart shows the price moving inside a year (offsets under allowances,
        // both rising), so a candle stands for a virtual month, not a whole year.
        ProductBookView book = View.Books[0] with
        {
            Prices =
            [
                new PeriodPriceView(1, 1, 98m, 101m, 97m, 100m, 1_000m),
                new PeriodPriceView(1, 4, 104m, 108m, 103m, 107m, 2_000m),
            ],
        };

        using JsonDocument prices = JsonDocument.Parse(
            CarbonSim.Web.Player.Charts.Prices([book], 3, 100m, "Auction floor price", year => $"Year {year}"));
        JsonElement candles = prices.RootElement.GetProperty("series")[0].GetProperty("data");

        prices.RootElement.GetProperty("xAxis").GetProperty("data").GetArrayLength().Should().Be(36, "three years of twelve months");
        candles[0].ValueKind.Should().Be(JsonValueKind.Array, "January of year 1 traded");
        candles[1].GetString().Should().Be("-", "February did not");
        candles[3].ValueKind.Should().Be(JsonValueKind.Array, "April traded");
    }

    [Fact]
    public async Task The_shell_reports_what_a_control_did()
    {
        Navigate($"/run/{SimulationId:D}/auction");

        IRenderedComponent<MainLayout> layout = RenderComponent<MainLayout>(parameters => parameters
            .Add(component => component.Body, (RenderFragment)(_ => { })));
        PlayerContext context = Services.GetRequiredService<PlayerContext>();

        await layout.InvokeAsync(() => context.ActAsync(
            session => session.PlaceBidAsync(SimulationId, 1, 1, 104m, 1_000m),
            Localizer(Services)["PlaceBid"].Value));

        layout.Markup.Should().Contain(Localizer(Services)["PlaceBid"].Value);
        layout.FindAll(".notice button").Should().NotBeEmpty("a notice can be dismissed");
    }

    [Fact]
    public async Task A_notice_stays_through_the_refresh_until_it_is_dismissed()
    {
        Navigate($"/run/{SimulationId:D}/auction");

        IRenderedComponent<MainLayout> layout = RenderComponent<MainLayout>(parameters => parameters
            .Add(component => component.Body, (RenderFragment)(_ => { })));
        PlayerContext context = Services.GetRequiredService<PlayerContext>();
        string placed = Localizer(Services)["PlaceBid"].Value;

        await layout.InvokeAsync(() => context.ActAsync(session => session.PlaceBidAsync(SimulationId, 1, 1, 104m, 1_000m), placed));

        // The shell re-reads the run every second; that must not wipe what the control said.
        await layout.InvokeAsync(context.RefreshAsync);

        context.Snapshot.Status.Should().Be(placed);
        layout.Find(".notice").TextContent.Should().Contain(placed);

        await layout.InvokeAsync(context.ClearNotice);

        layout.FindAll(".notice").Should().BeEmpty();
    }

    [Fact]
    public void A_settled_year_reads_as_compliant_and_has_nothing_left_to_count_down()
    {
        View = View with
        {
            Position = View.Position with { Shortfall = 0m, Settled = true },
            State = "YearEnded",
            AuctionOpen = false,
            TimeLeftInYear = TimeSpan.Zero,
            TimeToAuctionOpen = TimeSpan.Zero,
            TimeToAuctionClose = TimeSpan.Zero,
        };

        IRenderedComponent<Dashboard> dashboard = RenderComponent<Dashboard>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));
        IRenderedComponent<ProgressStrip> strip = RenderComponent<ProgressStrip>(parameters =>
            parameters.AddCascadingValue(Snapshot));
        IRenderedComponent<TopBar> bar = RenderComponent<TopBar>(parameters =>
            parameters.AddCascadingValue(Snapshot));

        dashboard.Markup.Should().Contain(Localizer(Services)["Compliant"].Value);
        bar.Markup.Should().Contain(Localizer(Services)["Compliant"].Value);
        strip.Markup.Should().NotContain(
            Localizer(Services)["AuctionStartsIn", string.Empty].Value.Trim(),
            "a closed year is not counting down to an auction");
        strip.Markup.Should().NotContain(Localizer(Services)["AuctionEndsIn", string.Empty].Value.Trim());
    }

    [Fact]
    public void A_warned_halt_counts_down_on_every_players_screen()
    {
        // The trainer's warning is the room's last chance to trade, so the strip says how long is left.
        View = View with { TimeToHalt = TimeSpan.FromSeconds(25) };

        IRenderedComponent<ProgressStrip> strip = RenderComponent<ProgressStrip>(parameters =>
            parameters.AddCascadingValue(Snapshot));

        strip.Find(".banner-halt-warning").TextContent.Should().Contain(
            Localizer(Services)["TradingHaltsIn", Display.Duration(TimeSpan.FromSeconds(25))].Value);
    }

    [Fact]
    public void The_offer_form_lists_the_companies_people_play_before_the_bots()
    {
        View = View with
        {
            Counterparties =
            [
                new CounterpartyView(9, "Delta Unit 1", "Delta Power", "Power", Automated: false),
                new CounterpartyView(99, "Red River Unit 1", "Red River Power", "Cement", Automated: true),
                new CounterpartyView(100, "AI Steel 1", "AI Steel One", "Steel", Automated: true),
            ],
        };

        IRenderedComponent<Otc> page = RenderComponent<Otc>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        string[] groups = [.. page.FindAll("#counterparty-unit optgroup").Select(group => group.GetAttribute("label") ?? string.Empty)];

        groups.Should().Equal(
            [Localizer(Services)["HumanCompanies"].Value, "Cement", "Steel"],
            "a person finds the other humans before the machines");
        List<string> options = [.. page.FindAll("#counterparty-unit option").Select(option => option.TextContent.Trim())];

        options.Should().Contain("Delta Unit 1 (Delta Power)");
        options.Should().OnlyContain(option => !option.Contains("(Delta Power) (Delta Power)", StringComparison.Ordinal));
        options.Count(option => option.EndsWith("(AI)", StringComparison.Ordinal)).Should().Be(2, "the bots are marked");
    }

    private void Navigate(string address) =>
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
}
