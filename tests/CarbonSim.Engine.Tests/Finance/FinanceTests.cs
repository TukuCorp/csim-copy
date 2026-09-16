using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Finance;

public sealed class FinanceTests
{
    private const decimal Limit = 500_000m;

    private static Simulation Build(decimal capital = 1_000_000m, decimal overdraftLimit = Limit)
    {
        return TestSimulation.Build(capital: capital, overdraftLimit: overdraftLimit);
    }

    [Fact]
    public void Every_movement_of_money_is_recorded_with_its_year_and_category()
    {
        Simulation simulation = Build();
        Company company = simulation.FindCompany(1);

        simulation.Cash.Deposit(company, 250m, CashCategory.OperatingProfit, "Year 1 trading");
        simulation.Cash.Withdraw(company, 100m, CashCategory.AbatementCapital, "Boiler retrofit");

        simulation.Cash.Movements.Should().HaveCount(2);
        simulation.Cash.Movements[0].Category.Should().Be(CashCategory.OperatingProfit);
        simulation.Cash.Movements[0].Amount.Should().Be(250m);
        simulation.Cash.Movements[0].Year.Should().Be(simulation.CurrentYear);
        simulation.Cash.Movements[1].Description.Should().Be("Boiler retrofit");
        company.Capital.Should().Be(1_000_150m);
    }

    [Fact]
    public void A_company_cannot_spend_past_its_credit_limit()
    {
        Simulation simulation = Build();

        Action tooFar = () => simulation.Cash.Withdraw(
            company: simulation.FindCompany(1),
            amount: 1_500_001m,
            category: CashCategory.SecondaryPurchase,
            description: "Vintage 1");

        tooFar.Should().Throw<InvalidOperationException>().WithMessage("*credit limit*");
        simulation.FindCompany(1).Capital.Should().Be(1_000_000m);

        // Exactly the limit is allowed: capital 1,000,000 + credit 500,000.
        simulation.Cash.Withdraw(simulation.FindCompany(1), 1_500_000m, CashCategory.SecondaryPurchase, "Vintage 1");
        simulation.FindCompany(1).Capital.Should().Be(-Limit);
        simulation.Finance.AvailableOverdraft(simulation.FindCompany(1)).Should().Be(0m);
    }

    [Fact]
    public void Money_set_aside_for_an_order_is_not_available_to_spend_elsewhere()
    {
        Simulation simulation = Build();
        Company company = simulation.FindCompany(1);

        simulation.Cash.Escrow(company, 400_000m);

        simulation.Cash.Available(company).Should().Be(600_000m);
        simulation.Cash.Escrowed(company).Should().Be(400_000m);
        company.Capital.Should().Be(1_000_000m, "escrowed money is still the company's until it is spent");

        Action tooFar = () => simulation.Cash.Withdraw(company, 1_100_001m, CashCategory.SecondaryPurchase, "Vintage 1");
        tooFar.Should().Throw<InvalidOperationException>();

        simulation.Cash.ReleaseEscrow(company, 400_000m);
        simulation.Cash.Available(company).Should().Be(1_000_000m);
    }

    [Fact]
    public void A_payment_from_escrow_hands_the_money_to_the_seller()
    {
        Simulation simulation = Build();
        Company buyer = simulation.FindCompany(1);
        Company seller = simulation.FindCompany(2);
        simulation.Cash.Escrow(buyer, 150_000m);

        simulation.Cash.SettleEscrow(buyer, seller, 120_000m, CashCategory.SecondaryPurchase, "Vintage 1 trade");

        simulation.Cash.Escrowed(buyer).Should().Be(30_000m);
        buyer.Capital.Should().Be(880_000m);
        seller.Capital.Should().Be(1_120_000m);
        simulation.Cash.Movements.Select(movement => movement.Category)
            .Should().Equal(CashCategory.SecondaryPurchase, CashCategory.InstrumentSale);
    }

    [Fact]
    public void A_regulators_charge_is_taken_whatever_the_credit_limit()
    {
        Simulation simulation = Build();
        Company company = simulation.FindCompany(1);

        simulation.Cash.Charge(company, 2_000_000m, CashCategory.Penalty, "Year 1 shortfall");

        company.Capital.Should().Be(-1_000_000m);
        simulation.Finance.AvailableOverdraft(company).Should().Be(0m, "credit is used up before the charge lands");
    }

    [Fact]
    public void A_buy_order_escrows_its_cash_and_refuses_what_the_company_cannot_afford()
    {
        Simulation simulation = Build(capital: 20_000m);
        Exchange exchange = new(simulation);
        Product vintage1 = Product.Allowance(1);
        Unit buyer = simulation.FindUnit(4);

        Action tooBig = () => exchange.Place(new OrderRequest(
            buyer, vintage1, OrderSide.Buy, OrderKind.Limit, 5_000m, Price: 105m));

        tooBig.Should().Throw<InvalidOperationException>().WithMessage("*credit limit*");

        Order order = exchange.Place(new OrderRequest(buyer, vintage1, OrderSide.Buy, OrderKind.Limit, 100m, Price: 105m));

        simulation.Cash.Escrowed(buyer.Company).Should().Be(10_500m);
        exchange.Cancel(buyer.Company, order.Id);
        simulation.Cash.Escrowed(buyer.Company).Should().Be(0m);
        buyer.Company.Capital.Should().Be(20_000m);
    }

    [Fact]
    public void A_market_buy_escrows_enough_for_the_band_and_hands_back_what_it_does_not_use()
    {
        Simulation simulation = Build(capital: 20_000m);
        Exchange exchange = new(simulation);
        Product vintage1 = Product.Allowance(1);
        simulation.Ledger.Grant(simulation.FindCompany(1), vintage1, 1_000m);
        exchange.Place(new OrderRequest(simulation.FindUnit(1), vintage1, OrderSide.Sell, OrderKind.Limit, 100m, Price: 100m));

        Order buy = exchange.Place(new OrderRequest(simulation.FindUnit(4), vintage1, OrderSide.Buy, OrderKind.Market, 100m));

        buy.Status.Should().Be(OrderStatus.Filled);
        buy.Company.Capital.Should().Be(10_000m, "100 tonnes at the resting price of 100");
        simulation.Cash.Escrowed(buy.Company).Should().Be(0m, "the rest of the band-wide escrow came back");
    }

    [Fact]
    public void An_auction_bid_escrows_its_cost_and_hands_back_the_difference_at_the_clearing_price()
    {
        Simulation simulation = Build(capital: 30_000_000m);
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);
        Auction auction = schedule.ForSection(1, 1);
        Unit unit = simulation.FindUnit(1);

        Action tooBig = () => auction.PlaceBid(unit, 1, 300m, 200_000m);

        tooBig.Should().Throw<InvalidOperationException>().WithMessage("*credit limit*");

        auction.PlaceBid(unit, 1, 150m, 100_000m);
        simulation.Cash.Escrowed(unit.Company).Should().Be(15_000_000m);

        AuctionResult result = auction.Clear().Single();

        result.ClearingPrice.Should().Be(100m, "the auction was under-subscribed");
        unit.Company.Capital.Should().Be(20_000_000m, "15,000,000 was set aside, 10,000,000 was spent, 5,000,000 came back");
        simulation.Cash.Escrowed(unit.Company).Should().Be(0m);
    }

    [Fact]
    public void An_unsuccessful_bid_gets_its_money_back()
    {
        Simulation simulation = Build(capital: 200_000_000m);
        Auction auction = AuctionSchedule.Build(simulation).ForSection(1, 1);
        Unit winner = simulation.FindUnit(1);
        Unit loser = simulation.FindUnit(4);
        decimal loserCapital = loser.Company.Capital;
        auction.PlaceBid(winner, 1, 200m, 237_500m);
        auction.PlaceBid(loser, 1, 100m, 237_500m);

        AuctionResult result = auction.Clear().Single();

        result.Rejected.Should().ContainSingle().Which.Unit.Should().BeSameAs(loser);
        loser.Company.Capital.Should().Be(loserCapital, "a bid that won nothing costs nothing");
        simulation.Cash.Escrowed(loser.Company).Should().Be(0m);
        winner.Company.Capital.Should().Be(200_000_000m - (237_500m * 200m));
    }

    [Fact]
    public void An_accepted_offer_is_refused_when_the_buyer_cannot_pay()
    {
        Simulation simulation = Build(capital: 1_000_000m);
        OtcMarket otc = new(simulation);
        Product vintage1 = Product.Allowance(1);
        simulation.Ledger.Grant(simulation.FindCompany(1), vintage1, 1_000m);
        OtcOffer offer = otc.Send(simulation.FindUnit(1), simulation.FindUnit(4), vintage1, price: 2_000m, volume: 1_000m);

        Action accept = () => otc.Accept(simulation.FindUnit(4), offer.Id);

        accept.Should().Throw<InvalidOperationException>().WithMessage("*credit limit*");
        offer.State.Should().Be(OtcOfferState.Pending);
        simulation.Ledger.Escrowed(simulation.FindCompany(1), vintage1).Should().Be(1_000m, "the seller keeps the instruments set aside");
    }

    [Fact]
    public void Normal_operating_profit_grows_when_abatement_earns_more_than_it_costs()
    {
        Simulation simulation = Build();
        CompanyFinance finance = new(simulation);
        Unit unit = simulation.FindUnit(1);
        AbatementOption earner = unit.AbatementOptions.Single(option => option.Code == "P3");
        unit.NormalOperatingProfit.Should().BeGreaterThan(0m);

        decimal before = finance.NopWithAbatement(simulation.FindCompany(1), 2);
        simulation.Abatements.Implement(unit, earner, year: 1);

        finance.NopWithAbatement(simulation.FindCompany(1), 2).Should().Be(before + earner.AnnualNetRevenue);
    }

    [Fact]
    public void Net_revenue_is_what_the_year_did_to_the_companys_cash()
    {
        Simulation simulation = Build();
        CompanyFinance finance = new(simulation);
        Company company = simulation.FindCompany(1);
        simulation.Cash.Deposit(company, 40_000m, CashCategory.InstrumentSale, "Sold 400 t");
        simulation.Cash.Charge(company, 5_000m, CashCategory.Penalty, "Shortfall");
        simulation.Cash.Charge(company, 1_000m, CashCategory.Fine, "Late surrender");

        finance.NetRevenue(company, 0).Should().Be(34_000m);
        finance.NetRevenue(simulation.FindCompany(2), 0).Should().Be(0m);
    }

    [Fact]
    public void Closing_a_year_charges_interest_then_pays_the_operating_profit()
    {
        Simulation simulation = Build(capital: 100_000m, overdraftLimit: 1_000_000m);
        StartClock(simulation);
        CompanyFinance finance = new(simulation);
        Company company = simulation.FindCompany(1);
        simulation.Cash.Withdraw(company, 400_000m, CashCategory.AbatementCapital, "Retrofit");
        company.Capital.Should().Be(-300_000m);

        finance.CloseYear(1);

        finance.Interest(company, 1).Should().Be(-21_000m, "7% of the 300,000 borrowed");
        company.Capital.Should().Be(-300_000m - 21_000m + finance.NormalOperatingProfit(company, 1));
        simulation.Cash.Movements.Should().Contain(movement => movement.Category == CashCategory.OperatingProfit);
    }

    [Fact]
    public void A_company_in_the_black_pays_no_interest()
    {
        Simulation simulation = Build();
        StartClock(simulation);
        CompanyFinance finance = new(simulation);

        finance.CloseYear(1);

        finance.Interest(simulation.FindCompany(1), 1).Should().Be(0m);
    }

    /// <summary>Money movements are stamped with the year the simulation is in, so the clock has to run.</summary>
    private static void StartClock(Simulation simulation)
    {
        new SimulationClock(simulation, new TestTimeProvider()).Start();
    }
}
