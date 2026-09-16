using System.Text.Json.Nodes;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Scenarios;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Scenarios;

public sealed class ScenarioLoaderTests
{
    private const string Valid = """
        {
          "name": "Two sector test scenario",
          "seed": 20240916,
          "parameters": {
            "cap": 1000000,
            "annualCapReductionRate": 0.03,
            "freeAllocationShare": 0.90,
            "years": 3,
            "offsetUsageLimit": 0.10,
            "bankingLimit": 1.0,
            "penaltyPerTonne": 300000,
            "penaltyAllowanceDebit": 1,
            "auctionFloorPrice": 100000,
            "auctionCeilingPrice": 300000,
            "auctionsPerYear": 4,
            "yearLengthMinutes": 20,
            "auctionDurationMinutes": 3,
            "tradingOpenShareOfYear": 0.45,
            "volatilityBand": 0.10,
            "overdraftInterestRate": 0.07,
            "bausGrowthBySector": [
              { "sector": "Power", "minAnnualRate": 0.02, "maxAnnualRate": 0.06 },
              { "sector": "Cement", "minAnnualRate": 0.02, "maxAnnualRate": 0.04 }
            ]
          },
          "sectors": [
            {
              "name": "Power",
              "emissionShare": 0.7,
              "abatementOptions": [
                { "code": "P1", "name": "Boiler retrofit", "annualReductionShare": 0.05, "upfrontCostPerTonne": 100, "annualNetRevenuePerTonne": -2, "implementationYears": 1, "lifetimeYears": 10 }
              ]
            },
            {
              "name": "Cement",
              "emissionShare": 0.3,
              "abatementOptions": [
                { "code": "C1", "name": "Kiln fuel switch", "annualReductionShare": 0.04, "upfrontCostPerTonne": 250, "annualNetRevenuePerTonne": 5, "implementationYears": 2, "lifetimeYears": 8 }
              ]
            }
          ],
          "companies": [
            {
              "name": "EVN Genco 1",
              "sector": "Power",
              "ownerKind": "human",
              "capital": 500000000,
              "overdraftLimit": 250000000,
              "units": [ { "name": "Phu My 1", "baselineEmissions": 600000, "normalOperatingProfit": 24000000 } ]
            },
            {
              "name": "Ha Tien Cement",
              "sector": "Cement",
              "ownerKind": "ai",
              "capital": 250000000,
              "overdraftLimit": 100000000,
              "units": [ { "name": "Kien Giang", "baselineEmissions": 300000, "normalOperatingProfit": 12000000 } ]
            }
          ]
        }
        """;

    /// <summary>Parses the valid fixture, applies <paramref name="mutate"/>, and re-serialises it.</summary>
    private static string Broken(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(Valid)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static JsonObject Company(JsonObject root, int index)
    {
        return root["companies"]!.AsArray()[index]!.AsObject();
    }

    [Fact]
    public void Loads_the_company_unit_and_player_graph()
    {
        Simulation simulation = ScenarioLoader.LoadJson(Valid, "test.json");

        simulation.State.Should().Be(SimulationState.Pending);
        simulation.Companies.Should().HaveCount(2);
        simulation.Units.Should().HaveCount(2);
        simulation.Players.Should().HaveCount(2);

        Unit unit = simulation.FindUnit(1);
        unit.Name.Should().Be("Phu My 1");
        unit.BaselineEmissions.Should().Be(600_000m);
        unit.Company.Name.Should().Be("EVN Genco 1");
        unit.Sector.Name.Should().Be("Power");

        simulation.FindPlayer(1).Kind.Should().Be(PlayerKind.Human);
        simulation.FindPlayer(2).Kind.Should().Be(PlayerKind.Ai);
        simulation.FindPlayer(2).Name.Should().Be("Ha Tien Cement");
        simulation.FindPlayer(2).Companies.Should().ContainSingle().Which.Name.Should().Be("Ha Tien Cement");
        simulation.FindCompany(1).Capital.Should().Be(500_000_000m);
        simulation.FindCompany(1).OverdraftLimit.Should().Be(250_000_000m);
        simulation.FindUnit(1).NormalOperatingProfit.Should().Be(24_000_000m);
        ParametersOf(simulation).OverdraftInterestRate.Should().Be(0.07m);
    }

    private static Parameters ParametersOf(Simulation simulation) => simulation.TradingSystems.Single().Parameters;

    [Fact]
    public void The_same_scenario_loaded_twice_produces_the_same_run()
    {
        Simulation first = ScenarioLoader.LoadJson(Valid, "test.json");
        Simulation second = ScenarioLoader.LoadJson(Valid, "test.json");
        Simulation other = ScenarioLoader.LoadJson(
            Broken(root => root["seed"] = 1UL),
            "test.json");

        first.Seed.Should().Be(20240916UL);
        first.Id.Should().Be(second.Id);
        first.Id.Should().NotBe(other.Id);
    }

    [Fact]
    public void A_company_without_capital_is_rejected()
    {
        string json = Broken(root => Company(root, 0).Remove("capital"));

        Action load = () => ScenarioLoader.LoadJson(json, "test.json");

        load.Should().Throw<ScenarioLoadException>()
            .Which.Errors.Should().ContainSingle()
            .Which.Should().Contain("capital");
    }

    [Fact]
    public void Assigns_ids_in_file_order()
    {
        Simulation simulation = ScenarioLoader.LoadJson(Valid, "test.json");

        simulation.Companies.Select(company => (company.Id, company.Name))
            .Should().Equal((1, "EVN Genco 1"), (2, "Ha Tien Cement"));
        simulation.Units.Select(unit => (unit.Id, unit.Name))
            .Should().Equal((1, "Phu My 1"), (2, "Kien Giang"));
        simulation.Players.Select(player => player.Id).Should().Equal(1, 2);
    }

    [Fact]
    public void Maps_the_parameters_block_onto_the_parameters_record()
    {
        Simulation simulation = ScenarioLoader.LoadJson(Valid, "test.json");
        Parameters parameters = simulation.TradingSystems.Single().Parameters;

        parameters.Cap.Should().Be(1_000_000m);
        parameters.AnnualCapReductionRate.Should().Be(0.03m);
        parameters.FreeAllocationShare.Should().Be(0.90m);
        parameters.OffsetUsageLimit.Should().Be(0.10m);
        parameters.PenaltyPerTonne.Should().Be(300_000m);
        parameters.PenaltyAllowanceDebit.Should().Be(1m);
        parameters.AuctionsPerYear.Should().Be(4);
        parameters.YearLength.Should().Be(TimeSpan.FromMinutes(20));
        parameters.AuctionDuration.Should().Be(TimeSpan.FromMinutes(3));
        parameters.TradingOpenShareOfYear.Should().Be(0.45m);
        parameters.VolatilityBand.Should().Be(0.10m);
        parameters.Years.Should().Be(3);
        parameters.BausGrowthBySector.Should().Equal(
            new SectorBausGrowth("Power", 0.02m, 0.06m),
            new SectorBausGrowth("Cement", 0.02m, 0.04m));
    }

    [Fact]
    public void Gives_every_unit_the_abatement_menu_of_its_sector_scaled_to_its_baseline()
    {
        Simulation simulation = ScenarioLoader.LoadJson(Valid, "test.json");

        Unit powerUnit = simulation.Units.Single(unit => unit.Sector.Name == "Power");
        AbatementOption power = powerUnit.AbatementOptions.Should().ContainSingle().Subject;
        power.Code.Should().Be("P1");
        power.AnnualReduction.Should().Be(30_000m);
        power.UpfrontCost.Should().Be(3_000_000m);
        power.AnnualNetRevenue.Should().Be(-60_000m);
        power.ImplementationYears.Should().Be(1);
        power.LifetimeYears.Should().Be(10);

        AbatementOption cement = simulation.Units.Single(unit => unit.Sector.Name == "Cement")
            .AbatementOptions.Should().ContainSingle().Subject;
        cement.AnnualReduction.Should().Be(12_000m);
        cement.UpfrontCost.Should().Be(3_000_000m);
        cement.AnnualNetRevenue.Should().Be(60_000m);
    }

    [Fact]
    public void Rejects_a_company_in_a_sector_the_scenario_does_not_define()
    {
        string json = Broken(root => Company(root, 1)["sector"] = "Aviation");

        Action load = () => ScenarioLoader.LoadJson(json, "test.json");

        load.Should().Throw<ScenarioLoadException>()
            .Which.Errors.Should().ContainSingle()
            .Which.Should().Contain("Aviation").And.Contain("Ha Tien Cement");
    }

    [Fact]
    public void Rejects_duplicate_company_names()
    {
        string json = Broken(root => Company(root, 1)["name"] = "EVN Genco 1");

        Action load = () => ScenarioLoader.LoadJson(json, "test.json");

        load.Should().Throw<ScenarioLoadException>()
            .Which.Errors.Should().ContainSingle()
            .Which.Should().Contain("EVN Genco 1");
    }

    [Fact]
    public void Rejects_sector_shares_that_do_not_add_up_to_the_cap()
    {
        string json = Broken(root => root["sectors"]!.AsArray()[1]!["emissionShare"] = 0.2m);

        Action load = () => ScenarioLoader.LoadJson(json, "test.json");

        load.Should().Throw<ScenarioLoadException>()
            .Which.Errors.Should().ContainSingle()
            .Which.Should().Contain("0.9").And.Contain("sum");
    }

    [Fact]
    public void Rejects_a_sector_without_a_bau_growth_range()
    {
        string json = Broken(root => root["parameters"]!["bausGrowthBySector"] = new JsonArray
        {
            new JsonObject { ["sector"] = "Power", ["minAnnualRate"] = 0.02m, ["maxAnnualRate"] = 0.06m },
        });

        Action load = () => ScenarioLoader.LoadJson(json, "test.json");

        load.Should().Throw<ScenarioLoadException>()
            .Which.Errors.Should().ContainSingle()
            .Which.Should().Contain("Cement");
    }

    [Fact]
    public void Rejects_parameters_out_of_range_and_reports_every_problem()
    {
        string json = Broken(root =>
        {
            root["parameters"]!["cap"] = 0;
            root["parameters"]!["auctionsPerYear"] = 0;
        });

        Action load = () => ScenarioLoader.LoadJson(json, "test.json");

        IReadOnlyList<string> errors = load.Should().Throw<ScenarioLoadException>().Which.Errors;

        errors.Should().HaveCount(2);
        errors.Should().Contain(error => error.Contains("cap", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("auctionsPerYear", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_a_company_without_units()
    {
        string json = Broken(root => Company(root, 1)["units"] = new JsonArray());

        Action load = () => ScenarioLoader.LoadJson(json, "test.json");

        load.Should().Throw<ScenarioLoadException>()
            .Which.Errors.Should().ContainSingle()
            .Which.Should().Contain("Ha Tien Cement").And.Contain("unit");
    }

    [Fact]
    public void Rejects_an_abatement_option_that_cannot_be_used()
    {
        string json = Broken(root => root["sectors"]!.AsArray()[0]!["abatementOptions"]!.AsArray()[0]!["lifetimeYears"] = 0);

        Action load = () => ScenarioLoader.LoadJson(json, "test.json");

        load.Should().Throw<ScenarioLoadException>()
            .Which.Errors.Should().ContainSingle()
            .Which.Should().Contain("lifetimeYears").And.Contain("P1");
    }

    [Fact]
    public void Reports_malformed_json_with_the_source_name()
    {
        Action load = () => ScenarioLoader.LoadJson("{ \"name\": ", "broken.json");

        load.Should().Throw<ScenarioLoadException>()
            .Which.Errors.Should().ContainSingle()
            .Which.Should().Contain("broken.json");
    }

    [Fact]
    public void Loads_the_shipped_vietnam_scenario_into_a_coherent_simulation()
    {
        Simulation simulation = ScenarioLoader.LoadFile(Path.Combine("scenarios", "vietnam-2024.json"));

        simulation.State.Should().Be(SimulationState.Pending);
        simulation.CurrentYear.Should().Be(0);
        simulation.Units.Should().HaveCount(242);
        simulation.Companies.Should().HaveCount(242);
        simulation.Players.Count(player => player.Kind == PlayerKind.Human).Should().Be(37);
        simulation.Sectors.Select(sector => sector.EmissionShare).Sum().Should().Be(1m);

        simulation.Sectors.Should().OnlyContain(sector => sector.EmissionShare > 0m);
        simulation.Units.Should().OnlyContain(unit => unit.BaselineEmissions > 0m);
        simulation.Units.Should().OnlyContain(unit => unit.AbatementOptions.Count > 0);
        simulation.Units.SelectMany(unit => unit.AbatementOptions)
            .Should().OnlyContain(option => option.AnnualReduction > 0m && option.LifetimeYears > 0);
        simulation.Companies.Should().OnlyContain(company => company.Units.Count > 0);
        simulation.TradingSystems.Single().Parameters.Validate().Should().BeEmpty();

        simulation.Players.Select(player => player.Name).Should().OnlyHaveUniqueItems();
        simulation.Units.Select(unit => unit.Name).Should().OnlyHaveUniqueItems();
    }
}
