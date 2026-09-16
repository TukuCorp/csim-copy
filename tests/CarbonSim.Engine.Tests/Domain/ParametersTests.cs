using System.Globalization;
using CarbonSim.Engine.Domain;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Domain;

public sealed class ParametersTests
{
    public static TheoryData<string, Func<Parameters, Parameters>> BrokenFields => new()
    {
        { "cap", parameters => parameters with { Cap = 0m } },
        { "annualCapReductionRate", parameters => parameters with { AnnualCapReductionRate = 1m } },
        { "freeAllocationShare", parameters => parameters with { FreeAllocationShare = 1.5m } },
        { "years", parameters => parameters with { Years = 0 } },
        { "offsetUsageLimit", parameters => parameters with { OffsetUsageLimit = -0.1m } },
        { "bankingLimit", parameters => parameters with { BankingLimit = 1.5m } },
        { "penaltyPerTonne", parameters => parameters with { PenaltyPerTonne = -1m } },
        { "penaltyAllowanceDebit", parameters => parameters with { PenaltyAllowanceDebit = -1m } },
        { "auctionFloorPrice", parameters => parameters with { AuctionFloorPrice = -1m } },
        { "auctionCeilingPrice", parameters => parameters with { AuctionCeilingPrice = 100_000m } },
        { "auctionsPerYear", parameters => parameters with { AuctionsPerYear = 0 } },
        { "yearLength", parameters => parameters with { YearLength = TimeSpan.Zero } },
        { "auctionDuration", parameters => parameters with { AuctionDuration = TimeSpan.FromMinutes(45) } },
        { "tradingOpenShareOfYear", parameters => parameters with { TradingOpenShareOfYear = 0m } },
        { "volatilityBand", parameters => parameters with { VolatilityBand = 0m } },
        { "bausGrowthBySector", parameters => parameters with { BausGrowthBySector = [] } },
        { "bausGrowthBySector", parameters => parameters with { BausGrowthBySector = [new SectorBausGrowth("Power", 0.06m, 0.02m)] } },
        { "bausGrowthBySector", parameters => parameters with { BausGrowthBySector = [new SectorBausGrowth("Power", -2m, 0.02m)] } },
    };

    [Fact]
    public void Scenario_parameters_for_the_vietnam_pilot_are_valid()
    {
        Parameters parameters = TestScenario.GreenParameters();

        parameters.Validate().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(BrokenFields))]
    public void Validation_reports_the_field_that_is_out_of_range(string field, Func<Parameters, Parameters> breakField)
    {
        Parameters broken = breakField(TestScenario.GreenParameters());

        broken.Validate().Should().ContainSingle()
            .Which.Should().ContainEquivalentOf(field);
    }

    [Fact]
    public void Validation_reports_every_broken_field_at_once()
    {
        Parameters broken = TestScenario.GreenParameters() with
        {
            Cap = -1m,
            Years = 0,
            VolatilityBand = 2m,
        };

        IReadOnlyList<string> errors = broken.Validate();

        errors.Should().HaveCount(3);
        errors.Should().Contain(error => error.Contains("cap", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("years", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("volatilityBand", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validation_messages_ignore_the_current_culture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Parameters broken = TestScenario.GreenParameters() with { FreeAllocationShare = 1.25m };

            broken.Validate().Should().ContainSingle().Which.Should().Contain("(was 1.25).");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_trading_system_cannot_run_on_invalid_parameters()
    {
        Parameters broken = TestScenario.GreenParameters() with { AuctionsPerYear = 0 };
        Sector power = new("Power", 1m);
        Player human = new(1, "Alice", PlayerKind.Human);
        Company company = new(1, "EVN Genco 1", power, human, 1_000_000m);

        Action build = () => new TradingSystem(1, "ETS", broken, [power], [company]);

        build.Should().Throw<ArgumentException>().WithMessage("*auctionsPerYear*");
    }
}
