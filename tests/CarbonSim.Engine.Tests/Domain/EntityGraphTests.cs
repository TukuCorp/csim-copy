using CarbonSim.Engine.Domain;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Domain;

/// <summary>
/// The entity graph is assembled by the scenario loader in production; these tests build the
/// same shapes by hand so wiring and aggregate contracts are pinned independently of JSON.
/// </summary>
public sealed class EntityGraphTests
{
    private static readonly Parameters Parameters = TestScenario.GreenParameters();

    private static Simulation BuildTwoSectorSimulation()
    {
        Sector power = new("Power", 0.6m);
        Sector cement = new("Cement", 0.4m);

        Player human = new(1, "Alice", PlayerKind.Human);
        Player bot = new(2, "AI 2", PlayerKind.Ai);

        Company evn = new(1, "EVN Genco 1", power, human, 500_000_000m);
        Company haTien = new(2, "Ha Tien Cement", cement, bot, 250_000_000m);

        Unit evnUnit = new(11, "Phu My 1", evn, 2_000_000m);
        Unit haTienUnit = new(12, "Kien Giang", haTien, 1_500_000m);
        evn.AddUnit(evnUnit);
        haTien.AddUnit(haTienUnit);

        TradingSystem main = new(1, "Vietnam ETS", Parameters, [power, cement], [evn, haTien]);

        return Simulation.Create("Test simulation", 42UL, [main], [human, bot]);
    }

    [Fact]
    public void Simulation_exposes_the_units_and_companies_of_its_trading_systems()
    {
        Simulation simulation = BuildTwoSectorSimulation();

        simulation.Units.Select(unit => unit.Name).Should().BeEquivalentTo("Phu My 1", "Kien Giang");
        simulation.Companies.Select(company => company.Name)
            .Should().BeEquivalentTo("EVN Genco 1", "Ha Tien Cement");
    }

    [Fact]
    public void Each_player_owns_the_companies_that_name_it_as_owner()
    {
        Simulation simulation = BuildTwoSectorSimulation();

        Player human = simulation.FindPlayer(1);
        Player bot = simulation.FindPlayer(2);

        human.Kind.Should().Be(PlayerKind.Human);
        bot.Kind.Should().Be(PlayerKind.Ai);
        human.Companies.Should().ContainSingle().Which.Name.Should().Be("EVN Genco 1");
        bot.Companies.Should().ContainSingle().Which.Name.Should().Be("Ha Tien Cement");
    }

    [Fact]
    public void Units_are_reachable_by_id_through_the_simulation_and_through_their_company()
    {
        Simulation simulation = BuildTwoSectorSimulation();

        Unit found = simulation.FindUnit(11);

        found.Name.Should().Be("Phu My 1");
        found.Company.Name.Should().Be("EVN Genco 1");
        found.Sector.Name.Should().Be("Power");
        found.Company.Units.Should().ContainSingle().Which.Should().BeSameAs(found);
    }

    [Fact]
    public void Lookups_reject_ids_and_names_that_are_not_in_the_simulation()
    {
        Simulation simulation = BuildTwoSectorSimulation();

        Action unknownUnit = () => simulation.FindUnit(999);
        Action unknownCompany = () => simulation.FindCompany(999);
        Action unknownPlayer = () => simulation.FindPlayer(999);
        Action unknownSector = () => simulation.FindSector("Aviation");

        unknownUnit.Should().Throw<KeyNotFoundException>().WithMessage("*999*");
        unknownCompany.Should().Throw<KeyNotFoundException>().WithMessage("*999*");
        unknownPlayer.Should().Throw<KeyNotFoundException>().WithMessage("*999*");
        unknownSector.Should().Throw<KeyNotFoundException>().WithMessage("*Aviation*");
    }

    [Fact]
    public void Duplicate_unit_ids_across_the_simulation_are_rejected()
    {
        Sector power = new("Power", 1m);
        Player human = new(1, "Alice", PlayerKind.Human);
        Company first = new(1, "First", power, human, 1_000m);
        Company second = new(2, "Second", power, human, 1_000m);
        first.AddUnit(new Unit(7, "A", first, 1_000m));
        second.AddUnit(new Unit(7, "B", second, 1_000m));
        TradingSystem system = new(1, "ETS", Parameters, [power], [first, second]);

        Action build = () => Simulation.Create("Clashing", 3UL, [system], [human]);

        build.Should().Throw<ArgumentException>().WithMessage("*7*");
    }

    [Fact]
    public void A_new_simulation_waits_to_start()
    {
        Simulation simulation = BuildTwoSectorSimulation();

        simulation.State.Should().Be(SimulationState.Pending);
        simulation.CurrentYear.Should().Be(0);
    }

    [Fact]
    public void Trading_system_reports_the_parameters_it_runs_under()
    {
        Simulation simulation = BuildTwoSectorSimulation();

        simulation.TradingSystems.Should().ContainSingle()
            .Which.Parameters.Should().BeSameAs(Parameters);
    }

    [Fact]
    public void A_sector_share_must_be_a_fraction_of_the_cap()
    {
        Action aboveOne = () => new Sector("Power", 1.2m);
        Action belowZero = () => new Sector("Power", -0.1m);

        aboveOne.Should().Throw<ArgumentOutOfRangeException>();
        belowZero.Should().Throw<ArgumentOutOfRangeException>();
        new Sector("Power", 1m).EmissionShare.Should().Be(1m);
    }

    [Fact]
    public void A_unit_cannot_have_negative_baseline_emissions()
    {
        Sector power = new("Power", 1m);
        Player human = new(1, "Alice", PlayerKind.Human);
        Company company = new(1, "EVN Genco 1", power, human, 1_000m);

        Action build = () => new Unit(1, "Phu My 1", company, -1m);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_sector_needs_a_name()
    {
        Action blank = () => new Sector("  ", 0.5m);

        blank.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_company_needs_capital_to_operate_with()
    {
        Sector power = new("Power", 1m);
        Player human = new(1, "Alice", PlayerKind.Human);

        Action noCapital = () => new Company(1, "EVN Genco 1", power, human, 0m);

        noCapital.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*capital*");
    }

    [Fact]
    public void A_simulation_identifier_comes_from_its_seed()
    {
        Simulation first = BuildTwoSectorSimulation();
        Simulation second = BuildTwoSectorSimulation();
        Simulation different = TestSimulation.Build(seed: 99UL);

        first.Seed.Should().Be(42UL);
        first.Id.Should().Be(second.Id);
        first.Random.Should().NotBeSameAs(second.Random);
        different.Id.Should().NotBe(first.Id);
    }
}
