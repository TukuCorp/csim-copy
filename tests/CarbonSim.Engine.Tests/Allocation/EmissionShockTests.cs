using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Snapshot;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Allocation;

/// <summary>
/// The administrator's end-of-year modification: a shock written into one unit's
/// business-as-usual path for one year, which every later reading of that unit must see.
/// </summary>
public sealed class EmissionShockTests
{
    [Fact]
    public void A_positive_shock_raises_the_units_emissions_for_that_year_only()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);
        decimal before = simulation.Allocation.BausEmissionsFor(unit, 2);
        decimal untouched = simulation.Allocation.BausEmissionsFor(simulation.FindUnit(2), 2);

        decimal after = simulation.Allocation.ApplyEmissionShock(unit, 2, 500_000m);

        after.Should().Be(before + 500_000m);
        simulation.Allocation.BausEmissionsFor(unit, 2).Should().Be(before + 500_000m);
        simulation.Allocation.BausEmissionsFor(unit, 1).Should().Be(unit.BaselineEmissions, "the shock names one year");
        simulation.Allocation.BausEmissionsFor(simulation.FindUnit(2), 2).Should().Be(untouched);
    }

    [Fact]
    public void A_negative_shock_lowers_emissions_and_never_passes_zero()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);

        simulation.Allocation.ApplyEmissionShock(unit, 1, -100_000m)
            .Should().Be(unit.BaselineEmissions - 100_000m);

        simulation.Allocation.ApplyEmissionShock(unit, 1, -decimal.MaxValue).Should().Be(0m);
        simulation.Allocation.BausEmissionsFor(unit, 1).Should().Be(0m);
    }

    [Fact]
    public void A_shock_survives_a_snapshot_round_trip()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(3);
        simulation.Allocation.ApplyEmissionShock(unit, 2, 750_000m);
        decimal expected = simulation.Allocation.BausEmissionsFor(unit, 2);

        SimulationSnapshot snapshot = SimulationSnapshots.Capture(
            simulation,
            new SimulationClock(simulation, new TestTimeProvider()),
            new Exchange(simulation),
            new OtcMarket(simulation),
            AuctionSchedule.Build(simulation));
        Simulation restored = SimulationSnapshots.Restore(snapshot, new TestTimeProvider()).Simulation;

        restored.Allocation.BausEmissionsFor(restored.FindUnit(3), 2).Should().Be(expected);
    }

    [Fact]
    public void A_shock_of_nothing_and_one_for_an_unknown_year_are_refused()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);

        Action nothing = () => simulation.Allocation.ApplyEmissionShock(unit, 1, 0m);
        Action beyond = () => simulation.Allocation.ApplyEmissionShock(unit, 99, 1m);

        nothing.Should().Throw<ArgumentOutOfRangeException>();
        beyond.Should().Throw<ArgumentOutOfRangeException>();
    }
}
