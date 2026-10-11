using CarbonSim.Web.Components.Shared;
using CarbonSim.Web.Player;
using CarbonSim.Web.Resources;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The long/short readout every screen shows: a company holding less than it emits reads Short, one
/// holding more reads Long, and a settled year that covered itself reads Compliant. The sign is the
/// trap - Standing.Position (and so PositionView.Position) is long-positive, while this label is
/// short-positive - so it is pinned here, in both languages.
/// </summary>
public sealed class DisplayPositionTests
{
    [Fact]
    public void A_company_holding_less_than_it_emits_reads_short()
    {
        using CultureScope culture = new("en");
        IStringLocalizer<SharedStrings> strings = Strings();

        // The T6 symptom: held 2,172,704 against forecast 2,550,258 is short by 377,554.
        PositionView view = View(held: 2_172_704m, emissions: 2_550_258m, shortfall: 377_554m);

        Display.Position(view, strings).Should().Be($"{strings["Short"]} {Display.Tonnes(377_554m)}");
    }

    [Fact]
    public void A_company_holding_more_than_it_emits_reads_long()
    {
        using CultureScope culture = new("en");
        IStringLocalizer<SharedStrings> strings = Strings();

        PositionView view = View(held: 1_500_000m, emissions: 1_000_000m, shortfall: 0m);

        Display.Position(view, strings).Should().Be($"{strings["Long"]} {Display.Tonnes(500_000m)}");
    }

    [Fact]
    public void A_settled_year_that_covered_itself_reads_compliant()
    {
        using CultureScope culture = new("en");
        IStringLocalizer<SharedStrings> strings = Strings();

        PositionView view = View(held: 1_000_000m, emissions: 1_000_000m, shortfall: 0m, settled: true);

        Display.Position(view, strings).Should().Be(strings["Compliant"]);
    }

    [Fact]
    public void A_settled_year_that_was_short_reads_short()
    {
        using CultureScope culture = new("en");
        IStringLocalizer<SharedStrings> strings = Strings();

        PositionView view = View(held: 1_250_000m, emissions: 1_500_000m, shortfall: 250_000m, settled: true);

        Display.Position(view, strings).Should().Be($"{strings["Short"]} {Display.Tonnes(250_000m)}");
    }

    [Fact]
    public void The_readout_is_Vietnamese_under_a_Vietnamese_culture()
    {
        using CultureScope culture = new("vi-VN");

        using ServiceProvider provider = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        IStringLocalizer<SharedStrings> strings = provider.GetRequiredService<IStringLocalizer<SharedStrings>>();

        Display.Position(View(held: 2_172_704m, emissions: 2_550_258m, shortfall: 377_554m), strings)
            .Should().Be($"{strings["Short"]} {Display.Tonnes(377_554m)}", "a short company reads Thiáº¿u");
        Display.Position(View(held: 1_500_000m, emissions: 1_000_000m, shortfall: 0m), strings)
            .Should().Be($"{strings["Long"]} {Display.Tonnes(500_000m)}", "a long company reads Thá»«a");
        Display.Position(View(held: 1_000_000m, emissions: 1_000_000m, shortfall: 0m, settled: true), strings)
            .Should().Be(strings["Compliant"], "a covered settled year reads Ä‘Ã£ tuÃ¢n thá»§");
    }

    /// <summary>A position view with the two figures the label reads, and nothing else it needs.</summary>
    private static PositionView View(decimal held, decimal emissions, decimal shortfall, bool settled = false) =>
        new(FreeAllocation: 0m, Held: held, ForecastEmissions: emissions, Reduction: 0m, Shortfall: shortfall, Settled: settled);

    private static IStringLocalizer<SharedStrings> Strings() =>
        new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<SharedStrings>>();
}

