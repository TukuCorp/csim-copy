using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Run;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Bots;

/// <summary>
/// A bot asks the same question the rest of the run asks: its shortfall is the standing the year
/// would be measured on. The fleet is automated so every company has a bot, and by the time it is
/// asked the fleet has quoted, which escrows the allowances behind resting sell orders - so the
/// cases that used to disagree (a resting sell order, a company holding offsets, a plain company)
/// are all in play. This is the evidence that the bot and Standing agree, and it exists because a
/// failing equivalence test has to be investigated rather than deleted.
/// </summary>
public sealed class BotStandingTests
{
    [Fact]
    public void A_bots_shortfall_is_the_standing_of_its_company_year()
    {
        Simulation simulation = TestSimulation.Build();

        foreach (Unit unit in simulation.Units)
        {
            unit.AutoTrade = true;
        }

        TestTimeProvider time = new();
        SimulationBots bots = SimulationBots.Create(simulation, BotSettings.For(BotDifficulty.Normal));
        SimulationRun run = SimulationRun.Create(simulation, time, bots);

        run.Start();

        // One company holds offsets, the others are plain; the fleet then quotes.
        simulation.Ledger.Grant(simulation.FindCompany(2), Product.Offset, 100_000m);

        time.Advance(TimeSpan.FromMinutes(3));
        run.Advance();

        Standing standing = new(simulation);
        int asked = 0;

        foreach (Company company in simulation.Companies)
        {
            ComplianceBot? bot = bots.Bots.FirstOrDefault(candidate => ReferenceEquals(candidate.Company, company));

            if (bot is null)
            {
                continue;
            }

            bot.Shortfall(simulation, 1).Should().Be(
                standing.Shortfall(company, 1),
                company.Name + " is asked the same question by its bot and by the year end");

            asked++;
        }

        asked.Should().Be(simulation.Companies.Count, "every company was automated, so every company has a bot");
    }
}
