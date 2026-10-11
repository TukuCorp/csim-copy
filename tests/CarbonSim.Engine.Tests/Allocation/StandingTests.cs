using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Allocation;

/// <summary>
/// Where a company stands in a year, asked of the one module that answers it. The table walks the
/// cases the five old answers disagreed about: which vintages count, whether an escrowed allowance
/// still counts, how far the offset limit reaches, and what a settled year says.
/// </summary>
public sealed class StandingTests
{
    /// <summary>What a company holds against a year, case by case.</summary>
    public static TheoryData<string, int, decimal[], decimal, decimal, decimal> Rows => new()
    {
        // name, year, grants of vintages 1..3, offsets granted, escrowed of the year's vintage,
        // and the allowance tonnes those grants should leave held.
        { "the compliance vintage counts", 2, [0m, 1_000m, 0m], 0m, 0m, 1_000m },
        { "older vintages count too", 2, [500m, 1_000m, 0m], 0m, 0m, 1_500m },
        { "a later vintage does not", 2, [500m, 1_000m, 9_000m], 0m, 0m, 1_500m },
        { "an escrowed allowance still counts", 2, [0m, 1_000m, 0m], 0m, 400m, 1_000m },
        { "offsets count up to the year's limit", 2, [0m, 1_000m, 0m], 9_000_000m, 0m, 1_000m },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Held_counts_the_right_instruments(
        string name,
        int year,
        decimal[] grants,
        decimal offsets,
        decimal escrowed,
        decimal expected)
    {
        Simulation simulation = TestSimulation.Build();
        Company company = simulation.FindCompany(1);
        Standing standing = new(simulation);

        for (int vintage = 1; vintage <= 3; vintage++)
        {
            if (grants[vintage - 1] > 0m)
            {
                simulation.Ledger.Grant(company, Product.Allowance(vintage), grants[vintage - 1]);
            }
        }

        if (offsets > 0m)
        {
            simulation.Ledger.Grant(company, Product.Offset, offsets);
        }

        if (escrowed > 0m)
        {
            simulation.Ledger.Escrow(company, Product.Allowance(year), escrowed);
        }

        // The limit is the offset usage limit of the company's own emissions, computed inside Standing.
        decimal usableOffsets = Math.Min(offsets, standing.OffsetLimit(company, year));

        standing.OffsetsUsable(company, year).Should().Be(usableOffsets, name);
        standing.Held(company, year).Should().Be(expected + usableOffsets, name);
        standing.Available(company, year).Should().Be(
            standing.Held(company, year) - escrowed,
            name + ": what is set aside is held but not available");
    }

    [Fact]
    public void Position_is_long_positive_and_shortfall_is_never_negative()
    {
        Simulation simulation = TestSimulation.Build();
        Company company = simulation.FindCompany(1);
        Standing standing = new(simulation);

        simulation.Ledger.GrantFreeAllocation(1);

        decimal emissions = standing.Emissions(company, 1);

        emissions.Should().BePositive("a live year emits something");
        standing.Position(company, 1).Should().Be(standing.Held(company, 1) - emissions);
        standing.Shortfall(company, 1).Should().Be(Math.Max(0m, emissions - standing.Held(company, 1)));

        // Hand the company everything it needs: the position turns long and the shortfall goes to zero.
        simulation.Ledger.Grant(company, Product.Allowance(1), emissions);

        standing.Position(company, 1).Should().BePositive("holding more than it emits is long");
        standing.Shortfall(company, 1).Should().Be(0m, "a covered year owes nothing");
        standing.Covered(company, 1).Should().BeTrue();
    }

    [Fact]
    public void A_settled_year_answers_from_its_settlement()
    {
        Simulation simulation = TestSimulation.Build();
        Company company = simulation.FindCompany(1);
        Standing standing = new(simulation);

        simulation.Ledger.GrantFreeAllocation(1);

        standing.IsSettled(company, 1).Should().BeFalse("the year has not been closed");

        simulation.Compliance.Reconcile(1);

        CompanyCompliance settlement = standing.Settlement(company, 1)
            ?? throw new InvalidOperationException("the year was reconciled, so it has a settlement");

        standing.IsSettled(company, 1).Should().BeTrue();
        standing.Shortfall(company, 1).Should().Be(settlement.Shortfall, "a settled year is not recomputed");
        standing.Position(company, 1).Should().Be(-settlement.Shortfall);
        standing.Covered(company, 1).Should().Be(settlement.IsCompliant);
    }

    [Fact]
    public void A_companys_emissions_are_the_sum_of_its_units()
    {
        Simulation simulation = TestSimulation.Build();
        Company company = simulation.FindCompany(1);
        Standing standing = new(simulation);

        company.Units.Should().HaveCountGreaterThan(1, "this company owns two units");
        standing.Emissions(company, 1).Should().Be(company.Units.Sum(unit => standing.Emissions(unit, 1)));
    }

    [Fact]
    public void A_unit_emits_what_its_business_as_usual_predicts()
    {
        Simulation simulation = TestSimulation.Build();
        Standing standing = new(simulation);
        Unit unit = simulation.FindUnit(1);

        decimal allocation = simulation.Allocation.FreeAllocationFor(unit, 1);
        decimal bau = simulation.Allocation.BausEmissionsFor(unit, 1);

        bau.Should().BeGreaterThan(allocation, "the unit emits more than its free allocation");
        standing.Emissions(unit, 1).Should().Be(bau, "nothing has been abated, so it emits its business as usual");
    }

    [Fact]
    public void A_year_outside_the_run_is_refused()
    {
        Simulation simulation = TestSimulation.Build();
        Standing standing = new(simulation);
        Company company = simulation.FindCompany(1);

        Action before = () => standing.Emissions(company, 0);
        Action after = () => standing.Shortfall(company, 99);

        before.Should().Throw<ArgumentOutOfRangeException>();
        after.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_year_nothing_has_been_granted_for_owes_the_whole_obligation()
    {
        Simulation simulation = TestSimulation.Build();
        Standing standing = new(simulation);
        Company company = simulation.FindCompany(1);
        decimal emissions = standing.Emissions(company, 1);

        standing.Held(company, 1).Should().Be(0m, "nothing has been granted yet");
        standing.Available(company, 1).Should().Be(0m);
        standing.Position(company, 1).Should().Be(-emissions, "holding nothing, it owes the whole obligation");
        standing.Shortfall(company, 1).Should().Be(emissions, "and that obligation is what it still has to cover");
    }
}
