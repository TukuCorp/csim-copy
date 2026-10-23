using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Run;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Engine.Snapshot;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Allocation;

/// <summary>
/// Free allocation can be set per sector (Spec p4, Sectors; catalog `FreeAllocationSectorAllocation`):
/// a sector with its own share gets that share of its slice of the cap, and a sector without one
/// keeps the system's share, so an exercise can shelter one sector while another buys at auction.
/// </summary>
public sealed class SectorFreeAllocationTests
{
    [Fact]
    public void A_sector_with_its_own_share_gets_that_share_of_its_slice_of_the_cap()
    {
        Simulation simulation = TestSimulation.Build(cementFreeAllocationShare: 0.50m);
        Sector power = simulation.Sectors.Single(sector => sector.Name == "Power");
        Sector cement = simulation.Sectors.Single(sector => sector.Name == "Cement");

        decimal cap = simulation.Allocation.CapForYear(1);
        decimal powerFree = simulation.Units.Where(unit => unit.Sector == power).Sum(unit => simulation.Allocation.FreeAllocationFor(unit, 1));
        decimal cementFree = simulation.Units.Where(unit => unit.Sector == cement).Sum(unit => simulation.Allocation.FreeAllocationFor(unit, 1));

        cementFree.Should().Be(Math.Round(cap * TestSimulation.CementShare * 0.50m));
        powerFree.Should().Be(Math.Round(cap * TestSimulation.PowerShare * 0.90m), "power keeps the system's 90%");
        simulation.Allocation.FreeAllocationForYear(1).Should().Be(powerFree + cementFree);
        simulation.Allocation.AuctionableVolumeForYear(1).Should().Be(cap - powerFree - cementFree, "what is not given away is auctioned");
    }

    [Fact]
    public void Without_a_sector_share_the_allocation_is_what_it_always_was()
    {
        Simulation plain = TestSimulation.Build();

        plain.Allocation.FreeAllocationForYear(1).Should().Be(Math.Round(plain.Allocation.CapForYear(1) * 0.90m));
    }

    [Fact]
    public void A_scenario_names_a_sectors_share_refuses_one_out_of_range_and_a_save_keeps_it()
    {
        string json = File.ReadAllText(Path.Combine("scenarios", "vietnam-2024.json"));
        Simulation simulation = ScenarioLoader.LoadJson(WithFirstSectorShare(json, "0.5"), "sector-free-share");
        SimulationRun run = SimulationRun.Create(simulation, new TestTimeProvider());

        simulation.Sectors[0].FreeAllocationShare.Should().Be(0.5m);
        simulation.Sectors.Skip(1).Should().OnlyContain(sector => sector.FreeAllocationShare == null);

        Simulation restored = SimulationSnapshots.Restore(
            SimulationSnapshots.Capture(simulation, run.Clock, run.Exchange, run.Otc, run.Auctions),
            new TestTimeProvider()).Simulation;

        restored.Sectors[0].FreeAllocationShare.Should().Be(0.5m);

        Action outOfRange = () => ScenarioLoader.LoadJson(WithFirstSectorShare(json, "1.5"), "sector-free-share-bad");

        outOfRange.Should().Throw<ScenarioLoadException>()
            .Which.Errors.Should().Contain(error => error.Contains("freeAllocationShare", StringComparison.Ordinal));
    }

    private static string WithFirstSectorShare(string json, string share)
    {
        int sectors = json.IndexOf("\"emissionShare\":", StringComparison.Ordinal);

        return json.Insert(sectors, $"\"freeAllocationShare\": {share}, ");
    }
}
