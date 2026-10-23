using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Compliance;

/// <summary>
/// The spec's surrender is "automatic or manual" (Spec p3; glossary, Surrender and Banking). In manual
/// mode a human company says what it surrenders, and the year end surrenders that and nothing else;
/// AI companies always surrender for themselves. The banking override banks every unused allowance
/// whatever the limit (catalog `AutomaticUnlimitedAllowanceBanking`).
/// </summary>
public sealed class ManualSurrenderTests
{
    private const decimal Obligation = 4_000_000m;

    private static readonly Product Vintage1 = Product.Allowance(1);

    private static (Simulation Simulation, Company Company) Manual()
    {
        Simulation simulation = TestSimulation.Build(automaticSurrender: false);
        return (simulation, simulation.FindCompany(1));
    }

    [Fact]
    public void A_manual_company_that_sets_nothing_surrenders_nothing()
    {
        (Simulation simulation, Company company) = Manual();
        simulation.Ledger.Grant(company, Vintage1, 5_000_000m);

        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.AllowancesSurrendered.Should().Be(0m);
        result.Shortfall.Should().Be(Obligation);
        result.Banked.Should().Be(Obligation, "what was not surrendered is banked up to the cap");
        result.Forfeited.Should().Be(1_000_000m);
    }

    [Fact]
    public void A_manual_company_surrenders_what_it_set()
    {
        (Simulation simulation, Company company) = Manual();
        simulation.Ledger.Grant(company, Vintage1, 5_000_000m);
        simulation.Ledger.Grant(company, Product.Offset, 500_000m);

        simulation.Compliance.SetSurrender(company, 1, allowances: 3_000_000m, offsets: 300_000m);
        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.OffsetsSurrendered.Should().Be(300_000m);
        result.AllowancesSurrendered.Should().Be(3_000_000m);
        result.Shortfall.Should().Be(700_000m);
        result.PenaltyCash.Should().Be(700_000m * 300m);
        simulation.Ledger.Available(company, Product.Offset).Should().Be(200_000m);
        result.Banked.Should().Be(2_000_000m);
    }

    [Fact]
    public void A_surrender_larger_than_the_obligation_surrenders_only_the_obligation()
    {
        (Simulation simulation, Company company) = Manual();
        simulation.Ledger.Grant(company, Vintage1, 6_000_000m);

        simulation.Compliance.SetSurrender(company, 1, allowances: 6_000_000m, offsets: 0m);
        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.AllowancesSurrendered.Should().Be(Obligation);
        result.Shortfall.Should().Be(0m);
        result.Banked.Should().Be(2_000_000m);
    }

    [Fact]
    public void A_surrender_the_company_can_no_longer_hold_surrenders_what_is_there()
    {
        (Simulation simulation, Company company) = Manual();
        simulation.Ledger.Grant(company, Vintage1, 3_000_000m);
        simulation.Compliance.SetSurrender(company, 1, allowances: 3_000_000m, offsets: 0m);
        simulation.Ledger.Consume(company, Vintage1, 1_000_000m);

        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.AllowancesSurrendered.Should().Be(2_000_000m);
        result.Shortfall.Should().Be(2_000_000m);
    }

    [Fact]
    public void Offsets_above_the_usage_limit_are_refused()
    {
        (Simulation simulation, Company company) = Manual();
        simulation.Ledger.Grant(company, Product.Offset, 1_000_000m);

        Action set = () => simulation.Compliance.SetSurrender(company, 1, allowances: 0m, offsets: 400_001m);

        set.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*400000*");
    }

    [Fact]
    public void More_than_the_company_holds_is_refused()
    {
        (Simulation simulation, Company company) = Manual();
        simulation.Ledger.Grant(company, Vintage1, 1_000_000m);
        simulation.Ledger.Grant(company, Product.Offset, 100_000m);

        Action allowances = () => simulation.Compliance.SetSurrender(company, 1, allowances: 1_000_001m, offsets: 0m);
        Action offsets = () => simulation.Compliance.SetSurrender(company, 1, allowances: 0m, offsets: 100_001m);
        Action negative = () => simulation.Compliance.SetSurrender(company, 1, allowances: -1m, offsets: 0m);

        allowances.Should().Throw<ArgumentOutOfRangeException>();
        offsets.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Setting_again_replaces_the_earlier_surrender()
    {
        (Simulation simulation, Company company) = Manual();
        simulation.Ledger.Grant(company, Vintage1, 5_000_000m);

        simulation.Compliance.SetSurrender(company, 1, allowances: 1_000_000m, offsets: 0m);
        simulation.Compliance.SetSurrender(company, 1, allowances: 4_000_000m, offsets: 0m);

        simulation.Compliance.InstructionFor(company, 1)!.Allowances.Should().Be(4_000_000m);
        simulation.Compliance.Reconcile(company, 1).AllowancesSurrendered.Should().Be(4_000_000m);
    }

    [Fact]
    public void Setting_a_surrender_is_refused_when_surrender_is_automatic()
    {
        Simulation simulation = TestSimulation.Build();
        Company company = simulation.FindCompany(1);
        simulation.Ledger.Grant(company, Vintage1, 5_000_000m);

        Action set = () => simulation.Compliance.SetSurrender(company, 1, allowances: 1m, offsets: 0m);

        set.Should().Throw<InvalidOperationException>().WithMessage("*automatic*");
    }

    [Fact]
    public void Setting_a_surrender_for_a_settled_year_is_refused()
    {
        (Simulation simulation, Company company) = Manual();
        simulation.Ledger.Grant(company, Vintage1, 5_000_000m);
        simulation.Compliance.Reconcile(company, 1);

        Action set = () => simulation.Compliance.SetSurrender(company, 1, allowances: 1m, offsets: 0m);

        set.Should().Throw<InvalidOperationException>().WithMessage("*already*");
    }

    [Fact]
    public void An_ai_company_surrenders_for_itself_in_manual_mode()
    {
        Simulation simulation = TestSimulation.Build(automaticSurrender: false);
        Company bot = simulation.FindCompany(3);
        simulation.Ledger.Grant(bot, Vintage1, 3_000_000m);

        CompanyCompliance result = simulation.Compliance.Reconcile(bot, 1);

        result.AllowancesSurrendered.Should().Be(2_000_000m);
        result.Shortfall.Should().Be(0m);
    }

    [Fact]
    public void Unlimited_banking_keeps_every_unused_allowance()
    {
        Simulation simulation = TestSimulation.Build(unlimitedBanking: true);
        Company company = simulation.FindCompany(1);
        simulation.Ledger.Grant(company, Vintage1, 9_000_000m);

        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.Banked.Should().Be(5_000_000m);
        result.Forfeited.Should().Be(0m);
        simulation.Ledger.Available(company, Vintage1).Should().Be(5_000_000m);
    }

    [Fact]
    public void The_plan_previews_the_automatic_year_end_without_moving_anything()
    {
        Simulation simulation = TestSimulation.Build();
        Company company = simulation.FindCompany(1);
        simulation.Ledger.Grant(company, Vintage1, 3_000_000m);
        simulation.Ledger.Grant(company, Product.Offset, 900_000m);

        SurrenderPlan plan = simulation.Compliance.Plan(company, 1);

        plan.Manual.Should().BeFalse();
        plan.Obligation.Should().Be(Obligation);
        plan.OffsetsAvailable.Should().Be(400_000m, "only the usage limit's worth may count");
        plan.AllowancesAvailable.Should().Be(3_000_000m);
        plan.Offsets.Should().Be(400_000m);
        plan.Allowances.Should().Be(3_000_000m);
        plan.Shortfall.Should().Be(600_000m);
        plan.EstimatedPenalty.Should().Be(600_000m * 300m);
        simulation.Ledger.Available(company, Vintage1).Should().Be(3_000_000m);
    }

    [Fact]
    public void The_plan_follows_the_manual_surrender_and_matches_the_year_end()
    {
        (Simulation simulation, Company company) = Manual();
        simulation.Ledger.Grant(company, Vintage1, 5_000_000m);

        simulation.Compliance.Plan(company, 1).Should().Match<SurrenderPlan>(plan =>
            plan.Manual && !plan.HasInstruction && plan.Shortfall == Obligation);

        simulation.Compliance.SetSurrender(company, 1, allowances: 3_500_000m, offsets: 0m);
        SurrenderPlan planned = simulation.Compliance.Plan(company, 1);
        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        planned.HasInstruction.Should().BeTrue();
        planned.Allowances.Should().Be(result.AllowancesSurrendered);
        planned.Shortfall.Should().Be(result.Shortfall);
        planned.EstimatedPenalty.Should().Be(result.PenaltyCash);
    }
}
