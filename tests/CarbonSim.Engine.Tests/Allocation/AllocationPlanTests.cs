using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Domain;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Allocation;

public sealed class AllocationPlanTests
{
    [Fact]
    public void The_cap_falls_at_the_configured_rate_and_is_rounded_to_whole_tonnes()
    {
        AllocationPlan plan = TestSimulation.Build().Allocation;

        plan.Years.Should().Equal(1, 2, 3);
        plan.CapForYear(1).Should().Be(9_500_000m);
        plan.CapForYear(2).Should().Be(9_215_000m);
        plan.CapForYear(3).Should().Be(8_938_550m);
    }

    [Fact]
    public void Only_the_configured_years_have_a_cap()
    {
        AllocationPlan plan = TestSimulation.Build().Allocation;

        Action yearZero = () => plan.CapForYear(0);
        Action beyond = () => plan.CapForYear(4);

        yearZero.Should().Throw<ArgumentOutOfRangeException>();
        beyond.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Free_allocation_is_the_configured_share_and_the_rest_is_auctioned()
    {
        AllocationPlan plan = TestSimulation.Build().Allocation;

        plan.FreeAllocationForYear(1).Should().Be(8_550_000m);
        plan.AuctionableVolumeForYear(1).Should().Be(950_000m);
        (plan.FreeAllocationForYear(1) + plan.AuctionableVolumeForYear(1)).Should().Be(plan.CapForYear(1));
    }

    [Fact]
    public void Sector_pots_follow_the_sector_shares()
    {
        Simulation simulation = TestSimulation.Build();
        AllocationPlan plan = simulation.Allocation;

        decimal power = simulation.Sectors.Single(sector => sector.Name == "Power").EmissionShare
            * plan.FreeAllocationForYear(1);
        decimal cement = simulation.Sectors.Single(sector => sector.Name == "Cement").EmissionShare
            * plan.FreeAllocationForYear(1);

        plan.FreeAllocationFor(simulation.FindUnit(1), 1).Should().Be(power / 3m);
        plan.FreeAllocationFor(simulation.FindUnit(1), 1).Should().Be(1_710_000m);
        plan.FreeAllocationFor(simulation.FindUnit(4), 1).Should().Be(cement);
    }

    [Fact]
    public void Units_share_their_sector_pot_in_proportion_to_their_baseline_emissions()
    {
        Simulation simulation = TestSimulation.Build();
        AllocationPlan plan = simulation.Allocation;

        // Power baselines are 2 Mt, 2 Mt and 2 Mt, so the pot is split three ways.
        plan.FreeAllocationFor(simulation.FindUnit(1), 1).Should().Be(1_710_000m);
        plan.FreeAllocationFor(simulation.FindUnit(2), 1).Should().Be(1_710_000m);
        plan.FreeAllocationFor(simulation.FindUnit(3), 1).Should().Be(1_710_000m);
        plan.FreeAllocationFor(simulation.FindCompany(1), 1).Should().Be(3_420_000m);
    }

    [Fact]
    public void Rounding_never_loses_or_invents_a_tonne()
    {
        Simulation simulation = UnevenSimulation();
        AllocationPlan plan = simulation.Allocation;

        decimal total = plan.FreeAllocationForYear(1);
        decimal allocated = simulation.Units.Sum(unit => plan.FreeAllocationFor(unit, 1));

        total.Should().Be(900_001m);
        allocated.Should().Be(total);
        plan.FreeAllocationFor(simulation.FindUnit(1), 1).Should().Be(300_001m, "the leftover tonne goes to the first unit when remainders tie");
        plan.FreeAllocationFor(simulation.FindUnit(3), 1).Should().Be(300_000m);
    }

    [Fact]
    public void Every_unit_draws_a_growth_rate_inside_its_sector_band()
    {
        Simulation simulation = TestSimulation.Build();
        AllocationPlan plan = simulation.Allocation;

        plan.BausGrowthFor(simulation.FindUnit(1)).Should().BeInRange(0.02m, 0.06m);
        plan.BausGrowthFor(simulation.FindUnit(3)).Should().BeInRange(0.02m, 0.06m);
        plan.BausGrowthFor(simulation.FindUnit(4)).Should().BeInRange(0.02m, 0.04m);
    }

    [Fact]
    public void Business_as_usual_emissions_grow_from_their_baseline()
    {
        Simulation simulation = TestSimulation.Build();
        AllocationPlan plan = simulation.Allocation;
        Unit unit = simulation.FindUnit(1);
        decimal growth = plan.BausGrowthFor(unit);

        plan.BausEmissionsFor(unit, 1).Should().Be(unit.BaselineEmissions);
        plan.BausEmissionsFor(unit, 2).Should().Be(Math.Round(unit.BaselineEmissions * (1m + growth), 0, MidpointRounding.AwayFromZero));
        (plan.BausEmissionsFor(unit, 2) > plan.BausEmissionsFor(unit, 1)).Should().BeTrue();
    }

    [Fact]
    public void The_same_seed_draws_the_same_growth_rates_and_another_seed_does_not()
    {
        Simulation first = TestSimulation.Build(seed: 5UL);
        Simulation second = TestSimulation.Build(seed: 5UL);
        Simulation other = TestSimulation.Build(seed: 6UL);

        first.Units.Select(unit => first.Allocation.BausGrowthFor(unit))
            .Should().Equal(second.Units.Select(unit => second.Allocation.BausGrowthFor(unit)));

        first.Units.Select(unit => first.Allocation.BausGrowthFor(unit))
            .Should().NotEqual(other.Units.Select(unit => other.Allocation.BausGrowthFor(unit)));
    }

    [Fact]
    public void The_free_cake_is_split_across_sectors_without_losing_or_inventing_a_tonne()
    {
        Simulation simulation = ThreeSectorSimulation();
        AllocationPlan plan = simulation.Allocation;

        plan.FreeAllocationForYear(1).Should().Be(91m, "90% of a 101 tonne cap rounds to 91");
        simulation.Units.Sum(unit => plan.FreeAllocationFor(unit, 1)).Should().Be(91m);
        simulation.Units.Select(unit => plan.FreeAllocationFor(unit, 1)).Should().Equal(37m, 27m, 27m);
    }

    private static Simulation ThreeSectorSimulation()
    {
        Sector first = new("Power", 0.4m);
        Sector second = new("Cement", 0.3m);
        Sector third = new("Steel", 0.3m);
        Player human = new(1, "Alice", PlayerKind.Human);
        Company company = new(1, "Three Sector Company", first, human, 10_000_000m);
        company.AddUnit(new Unit(1, "U1", company, 100_000m));

        List<Company> companies = [company];

        foreach ((int id, Sector sector) in new[] { (2, second), (3, third) })
        {
            Company other = new(id, $"{sector.Name} Company", sector, human, 10_000_000m);
            other.AddUnit(new Unit(id, $"U{id}", other, 100_000m));
            companies.Add(other);
        }

        TradingSystem system = new(
            1,
            "Three sector ETS",
            TestSimulation.Parameters() with
            {
                Cap = 101m,
                BausGrowthBySector =
                [
                    new SectorBausGrowth("Power", 0.02m, 0.06m),
                    new SectorBausGrowth("Cement", 0.02m, 0.06m),
                    new SectorBausGrowth("Steel", 0.02m, 0.06m),
                ],
            },
            [first, second, third],
            companies);

        return Simulation.Create("Three sector run", 17UL, [system], [human]);
    }

    private static Simulation UnevenSimulation()
    {
        Sector power = new("Power", 1m);
        Player human = new(1, "Alice", PlayerKind.Human);
        Company company = new(1, "Uneven Power", power, human, 10_000_000m);
        company.AddUnit(new Unit(1, "U1", company, 1_000_000m));
        company.AddUnit(new Unit(2, "U2", company, 1_000_000m));
        company.AddUnit(new Unit(3, "U3", company, 1_000_000m));

        TradingSystem system = new(
            1,
            "Uneven ETS",
            TestSimulation.Parameters() with { Cap = 1_000_001m },
            [power],
            [company]);

        return Simulation.Create("Uneven", 11UL, [system], [human]);
    }
}
