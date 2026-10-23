using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Run;
using CarbonSim.Engine.Snapshot;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Run;

/// <summary>
/// The trainer's end-of-year modifications to the system (Spec p2, "changes in allocations, emission
/// growth rates, offset use"; catalog `SystemModifications`): the offset usage limit, the banking limit
/// and the cash penalty move between years, the next reconciliation uses the new figures, and a save
/// keeps them.
/// </summary>
public sealed class SystemModificationTests
{
    private const decimal Obligation = 4_000_000m;

    [Fact]
    public void The_next_reconciliation_uses_the_new_offset_limit_banking_limit_and_penalty()
    {
        Simulation simulation = TestSimulation.Build();
        SimulationRun run = SimulationRun.Create(simulation, new TestTimeProvider());
        Company company = simulation.FindCompany(1);
        simulation.Ledger.Grant(company, Product.Offset, 1_000_000m);
        simulation.Ledger.Grant(company, Product.Allowance(1), 3_000_000m);

        run.ChangeComplianceRules(offsetUsageLimit: 0.05m, bankingLimit: 0.5m, penaltyPerTonne: 450m);
        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.OffsetsSurrendered.Should().Be(Obligation * 0.05m);
        result.AllowancesSurrendered.Should().Be(3_000_000m);
        result.Shortfall.Should().Be(Obligation - 200_000m - 3_000_000m);
        result.PenaltyCash.Should().Be(result.Shortfall * 450m);
        simulation.RulesRevision.Should().BePositive("the bots re-read the rules when the revision moves");
    }

    [Fact]
    public void The_banking_limit_caps_what_is_carried()
    {
        Simulation simulation = TestSimulation.Build();
        SimulationRun run = SimulationRun.Create(simulation, new TestTimeProvider());
        Company company = simulation.FindCompany(1);
        simulation.Ledger.Grant(company, Product.Allowance(1), 9_000_000m);

        run.ChangeComplianceRules(offsetUsageLimit: null, bankingLimit: 0.25m, penaltyPerTonne: null);
        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.Banked.Should().Be(Obligation * 0.25m);
        result.Forfeited.Should().Be(5_000_000m - (Obligation * 0.25m));
        simulation.Rules.OffsetUsageLimit.Should().Be(0.10m, "a value left out is left as it was");
        simulation.Rules.PenaltyPerTonne.Should().Be(300m);
    }

    [Theory]
    [InlineData(-0.1, null, null)]
    [InlineData(1.1, null, null)]
    [InlineData(null, -0.1, null)]
    [InlineData(null, 1.5, null)]
    [InlineData(null, null, -1.0)]
    public void Out_of_range_values_are_refused_and_nothing_moves(double? offsetLimit, double? bankingLimit, double? penalty)
    {
        Simulation simulation = TestSimulation.Build();
        SimulationRun run = SimulationRun.Create(simulation, new TestTimeProvider());

        Action change = () => run.ChangeComplianceRules((decimal?)offsetLimit, (decimal?)bankingLimit, (decimal?)penalty);

        change.Should().Throw<ArgumentOutOfRangeException>();
        simulation.Rules.OffsetUsageLimit.Should().Be(0.10m);
        simulation.Rules.BankingLimit.Should().Be(1.00m);
        simulation.Rules.PenaltyPerTonne.Should().Be(300m);
    }

    [Fact]
    public void A_save_keeps_the_changed_rules()
    {
        Simulation simulation = TestSimulation.Build();
        SimulationRun run = SimulationRun.Create(simulation, new TestTimeProvider());

        run.ChangeComplianceRules(0.15m, 0.75m, 500m);
        Parameters restored = SimulationSnapshots.Restore(
            SimulationSnapshots.Capture(simulation, run.Clock, run.Exchange, run.Otc, run.Auctions),
            new TestTimeProvider()).Simulation.Rules;

        restored.OffsetUsageLimit.Should().Be(0.15m);
        restored.BankingLimit.Should().Be(0.75m);
        restored.PenaltyPerTonne.Should().Be(500m);
    }
}
