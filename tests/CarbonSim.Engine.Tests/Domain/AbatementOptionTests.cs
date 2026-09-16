using CarbonSim.Engine.Domain;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Domain;

public sealed class AbatementOptionTests
{
    public static TheoryData<string, Func<AbatementOption>> RejectedOptions => new()
    {
        { "upfrontCost", () => new AbatementOption("P1", "Boiler retrofit", -1m, 1_000m, 1, 10, 0m) },
        { "annualReduction", () => new AbatementOption("P1", "Boiler retrofit", 1_000m, 0m, 1, 10, 0m) },
        { "implementationYears", () => new AbatementOption("P1", "Boiler retrofit", 1_000m, 1_000m, -1, 10, 0m) },
        { "lifetimeYears", () => new AbatementOption("P1", "Boiler retrofit", 1_000m, 1_000m, 1, 0, 0m) },
        { "code", () => new AbatementOption("  ", "Boiler retrofit", 1_000m, 1_000m, 1, 10, 0m) },
        { "name", () => new AbatementOption("P1", "  ", 1_000m, 1_000m, 1, 10, 0m) },
    };

    [Theory]
    [MemberData(nameof(RejectedOptions))]
    public void An_option_rejects_the_field_it_cannot_be_built_from(string field, Func<AbatementOption> build)
    {
        Action act = () => build();

        act.Should().Throw<ArgumentException>().WithMessage($"*{field}*");
    }

    [Fact]
    public void An_option_carries_its_economics()
    {
        AbatementOption option = new("P1", "Boiler retrofit", 4_000_000m, 1_000m, 1, 10, -50_000m);

        option.UpfrontCost.Should().Be(4_000_000m);
        option.AnnualReduction.Should().Be(1_000m);
        option.ImplementationYears.Should().Be(1);
        option.LifetimeYears.Should().Be(10);
        option.AnnualNetRevenue.Should().Be(-50_000m);
    }
}
