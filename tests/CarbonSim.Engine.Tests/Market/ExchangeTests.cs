using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Market;

/// <summary>
/// Books start anchored on the auction floor (100) with a 10% volatility band, so orders the
/// tests place sit inside 90..110 unless the test is about the band itself.
/// </summary>
public sealed class ExchangeTests
{
    private static readonly Product Vintage1 = Product.Allowance(1);

    private static (Simulation Simulation, Exchange Exchange) Build()
    {
        Simulation simulation = TestSimulation.Build();
        Exchange exchange = new(simulation);

        simulation.Ledger.Grant(simulation.FindCompany(1), Vintage1, 1_000_000m);
        simulation.Ledger.Grant(simulation.FindCompany(2), Vintage1, 1_000_000m);

        return (simulation, exchange);
    }

    private static OrderRequest Order(
        Simulation simulation,
        int unitId,
        OrderSide side,
        decimal volume,
        OrderKind kind = OrderKind.Limit,
        decimal? price = null,
        decimal? stopPrice = null,
        FillPolicy fillPolicy = FillPolicy.AllowPartial,
        Product? product = null)
    {
        return new OrderRequest(
            simulation.FindUnit(unitId),
            product ?? Vintage1,
            side,
            kind,
            volume,
            price,
            stopPrice,
            fillPolicy);
    }

    [Fact]
    public void A_limit_buy_that_crosses_a_resting_sell_trades_at_the_resting_price()
    {
        (Simulation simulation, Exchange exchange) = Build();

        Order sell = exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m, price: 105m));
        Order buy = exchange.Place(Order(simulation, 4, OrderSide.Buy, 100m, price: 110m));

        Trade trade = exchange.Book(Vintage1).Trades.Should().ContainSingle().Subject;
        trade.Price.Should().Be(105m, "the resting order sets the price");
        trade.Volume.Should().Be(100m);
        sell.Status.Should().Be(OrderStatus.Filled);
        buy.Status.Should().Be(OrderStatus.Filled);
        exchange.Book(Vintage1).LastTradePrice.Should().Be(105m);
    }

    [Fact]
    public void An_order_that_cannot_match_rests_on_the_book()
    {
        (Simulation simulation, Exchange exchange) = Build();

        Order sell = exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m, price: 105m));

        sell.Status.Should().Be(OrderStatus.Open);
        sell.Remaining.Should().Be(100m);
        exchange.Book(Vintage1).BestOffer.Should().Be(105m);
        exchange.Book(Vintage1).BestBid.Should().BeNull();
        exchange.Book(Vintage1).OpenOrders.Should().ContainSingle().Which.Should().BeSameAs(sell);
    }

    [Fact]
    public void A_partial_fill_leaves_the_rest_of_the_order_working()
    {
        (Simulation simulation, Exchange exchange) = Build();

        Order buy = exchange.Place(Order(simulation, 1, OrderSide.Buy, 100m, price: 100m));
        exchange.Place(Order(simulation, 4, OrderSide.Sell, 40m, price: 95m));

        buy.FilledVolume.Should().Be(40m);
        buy.Remaining.Should().Be(60m);
        buy.Status.Should().Be(OrderStatus.PartiallyFilled);
        exchange.Book(Vintage1).BestBid.Should().Be(100m);
        exchange.Book(Vintage1).Trades.Should().ContainSingle()
            .Which.Price.Should().Be(100m, "the resting buy sets the price");
    }

    [Fact]
    public void A_fill_or_kill_order_is_cancelled_when_the_book_cannot_cover_it()
    {
        (Simulation simulation, Exchange exchange) = Build();
        exchange.Place(Order(simulation, 1, OrderSide.Sell, 40m, price: 105m));

        Order buy = exchange.Place(Order(
            simulation, 4, OrderSide.Buy, 100m, price: 110m, fillPolicy: FillPolicy.FillOrKill));

        buy.Status.Should().Be(OrderStatus.Cancelled);
        buy.FilledVolume.Should().Be(0m);
        exchange.Book(Vintage1).Trades.Should().BeEmpty();
        exchange.Book(Vintage1).OpenOrders.Should().ContainSingle().Which.Side.Should().Be(OrderSide.Sell);
    }

    [Fact]
    public void A_fill_or_kill_order_executes_when_the_book_covers_it()
    {
        (Simulation simulation, Exchange exchange) = Build();
        exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m, price: 105m));

        Order buy = exchange.Place(Order(
            simulation, 4, OrderSide.Buy, 100m, price: 110m, fillPolicy: FillPolicy.FillOrKill));

        buy.Status.Should().Be(OrderStatus.Filled);
        exchange.Book(Vintage1).Trades.Should().ContainSingle().Which.Price.Should().Be(105m);
    }

    [Fact]
    public void A_fill_or_kill_market_order_is_killed_rather_than_partly_filled()
    {
        (Simulation simulation, Exchange exchange) = Build();
        exchange.Place(Order(simulation, 1, OrderSide.Sell, 40m, price: 105m));

        Order buy = exchange.Place(Order(
            simulation, 4, OrderSide.Buy, 100m, kind: OrderKind.Market, fillPolicy: FillPolicy.FillOrKill));

        buy.FilledVolume.Should().Be(0m);
        buy.Status.Should().Be(OrderStatus.Cancelled);
        exchange.Book(Vintage1).Trades.Should().BeEmpty();
    }

    [Fact]
    public void A_market_order_takes_what_is_on_the_book_and_the_rest_is_cancelled()
    {
        (Simulation simulation, Exchange exchange) = Build();
        exchange.Place(Order(simulation, 1, OrderSide.Sell, 40m, price: 105m));
        exchange.Place(Order(simulation, 2, OrderSide.Sell, 100m, price: 108m));

        Order buy = exchange.Place(Order(simulation, 4, OrderSide.Buy, 200m, kind: OrderKind.Market));

        buy.FilledVolume.Should().Be(140m);
        buy.Status.Should().Be(OrderStatus.Cancelled);
        exchange.Book(Vintage1).Trades.Select(trade => trade.Price).Should().Equal(105m, 108m);
        exchange.Book(Vintage1).OpenOrders.Should().BeEmpty("a market order never rests");
    }

    [Fact]
    public void An_order_priced_outside_the_volatility_band_is_rejected()
    {
        (Simulation simulation, Exchange exchange) = Build();

        Action tooHigh = () => exchange.Place(Order(simulation, 4, OrderSide.Buy, 100m, price: 111m));
        Action tooLow = () => exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m, price: 89m));

        tooHigh.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*band*");
        tooLow.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*band*");
        exchange.Book(Vintage1).OpenOrders.Should().BeEmpty();
    }

    [Fact]
    public void A_resting_order_the_market_moved_away_from_cannot_be_traded_through()
    {
        (Simulation simulation, Exchange exchange) = Build();
        exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m, price: 90m));
        exchange.Place(Order(simulation, 2, OrderSide.Sell, 100m, price: 105m));

        // The trade pulls the last price down to 90, which moves the band to 81..99.
        exchange.Place(Order(simulation, 4, OrderSide.Buy, 100m, price: 110m));
        exchange.Book(Vintage1).LastTradePrice.Should().Be(90m);

        Order market = exchange.Place(Order(simulation, 4, OrderSide.Buy, 100m, kind: OrderKind.Market));

        market.FilledVolume.Should().Be(0m, "the only offer left is above the band");
        market.Status.Should().Be(OrderStatus.Cancelled);
        exchange.Book(Vintage1).OpenOrders.Should().ContainSingle().Which.Price.Should().Be(105m);
    }

    [Fact]
    public void A_stop_order_waits_for_the_price_to_cross_it()
    {
        (Simulation simulation, Exchange exchange) = Build();
        exchange.Place(Order(simulation, 4, OrderSide.Buy, 100m, price: 95m));
        Order stop = exchange.Place(Order(
            simulation, 1, OrderSide.Sell, 50m, kind: OrderKind.StopLoss, stopPrice: 95m));

        stop.Status.Should().Be(OrderStatus.Open, "the last trade is still the reference price of 100");

        exchange.Place(Order(simulation, 2, OrderSide.Sell, 50m, kind: OrderKind.Market));

        exchange.Book(Vintage1).LastTradePrice.Should().Be(95m);
        stop.FilledVolume.Should().Be(50m);
        stop.Status.Should().Be(OrderStatus.Filled);
        exchange.Book(Vintage1).Trades.Should().HaveCount(2);
    }

    [Fact]
    public void A_stop_order_the_price_has_already_crossed_goes_to_market_at_once()
    {
        (Simulation simulation, Exchange exchange) = Build();
        exchange.Place(Order(simulation, 4, OrderSide.Buy, 100m, price: 100m));

        Order stop = exchange.Place(Order(
            simulation, 1, OrderSide.Sell, 100m, kind: OrderKind.StopLoss, stopPrice: 100m));

        stop.Status.Should().Be(OrderStatus.Filled);
        exchange.Book(Vintage1).Trades.Should().ContainSingle().Which.Price.Should().Be(100m);
    }

    [Fact]
    public void Selling_escrows_the_allowances_until_the_trade_or_the_cancellation()
    {
        (Simulation simulation, Exchange exchange) = Build();
        Company seller = simulation.FindCompany(1);

        Order sell = exchange.Place(Order(simulation, 1, OrderSide.Sell, 400m, price: 105m));

        simulation.Ledger.Escrowed(seller, Vintage1).Should().Be(400m);
        simulation.Ledger.Available(seller, Vintage1).Should().Be(999_600m);

        exchange.Cancel(seller, sell.Id);

        simulation.Ledger.Escrowed(seller, Vintage1).Should().Be(0m);
        simulation.Ledger.Available(seller, Vintage1).Should().Be(1_000_000m);
    }

    [Fact]
    public void A_trade_moves_instruments_and_money_between_the_two_companies()
    {
        (Simulation simulation, Exchange exchange) = Build();
        Company seller = simulation.FindCompany(1);
        Company buyer = simulation.FindCompany(2);
        decimal sellerCapital = seller.Capital;
        decimal buyerCapital = buyer.Capital;

        exchange.Place(Order(simulation, 1, OrderSide.Sell, 200m, price: 105m));
        exchange.Place(Order(simulation, 4, OrderSide.Buy, 200m, price: 105m));

        simulation.Ledger.Available(seller, Vintage1).Should().Be(999_800m);
        simulation.Ledger.Available(buyer, Vintage1).Should().Be(1_000_200m);
        seller.Capital.Should().Be(sellerCapital + (200m * 105m));
        buyer.Capital.Should().Be(buyerCapital - (200m * 105m));
    }

    [Fact]
    public void Selling_allowances_the_company_does_not_hold_is_refused()
    {
        (Simulation simulation, Exchange exchange) = Build();

        Action sell = () => exchange.Place(Order(simulation, 1, OrderSide.Sell, 2_000_000m, price: 105m));

        sell.Should().Throw<InvalidOperationException>().WithMessage("*insufficient*");
        exchange.Book(Vintage1).OpenOrders.Should().BeEmpty();
    }

    [Fact]
    public void A_limit_order_needs_a_price_and_a_stop_order_needs_a_stop_price()
    {
        (Simulation simulation, Exchange exchange) = Build();

        Action noPrice = () => exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m));
        Action marketWithPrice = () => exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m, kind: OrderKind.Market, price: 105m));
        Action stopWithoutStop = () => exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m, kind: OrderKind.StopLoss));
        Action noVolume = () => exchange.Place(Order(simulation, 1, OrderSide.Sell, 0m, price: 105m));

        noPrice.Should().Throw<ArgumentException>().WithMessage("*price*");
        marketWithPrice.Should().Throw<ArgumentException>().WithMessage("*price*");
        stopWithoutStop.Should().Throw<ArgumentException>().WithMessage("*stop*");
        noVolume.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*volume*");
    }

    [Fact]
    public void Orders_at_the_same_price_are_served_in_the_order_they_arrived()
    {
        (Simulation simulation, Exchange exchange) = Build();
        Order first = exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m, price: 105m));
        exchange.Place(Order(simulation, 2, OrderSide.Sell, 100m, price: 105m));

        Order buy = exchange.Place(Order(simulation, 4, OrderSide.Buy, 100m, price: 110m));

        buy.FilledVolume.Should().Be(100m);
        exchange.Book(Vintage1).Trades.Should().ContainSingle().Which.SellOrderId.Should().Be(first.Id);
        first.Status.Should().Be(OrderStatus.Filled);
    }

    [Fact]
    public void Each_product_has_its_own_book()
    {
        (Simulation simulation, Exchange exchange) = Build();
        Product vintage2 = Product.Allowance(2);
        simulation.Ledger.Grant(simulation.FindCompany(1), vintage2, 500m);
        simulation.Ledger.Grant(simulation.FindCompany(2), Product.Offset, 500m);

        exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m, price: 105m));
        exchange.Place(Order(simulation, 4, OrderSide.Buy, 100m, price: 105m));
        exchange.Book(Vintage1).LastTradePrice.Should().Be(105m);

        exchange.Book(vintage2).LastTradePrice.Should().Be(100m);
        exchange.Book(Product.Offset).LastTradePrice.Should().Be(100m);
        exchange.Book(vintage2).Trades.Should().BeEmpty();
        exchange.Book(Product.Offset).Trades.Should().BeEmpty();
    }

    [Fact]
    public void Offsets_trade_on_their_own_book_without_a_vintage()
    {
        (Simulation simulation, Exchange exchange) = Build();
        simulation.Ledger.Grant(simulation.FindCompany(2), Product.Offset, 500m);

        exchange.Place(Order(simulation, 4, OrderSide.Sell, 300m, price: 95m, product: Product.Offset));
        exchange.Place(Order(simulation, 1, OrderSide.Buy, 300m, price: 95m, product: Product.Offset));

        exchange.Book(Product.Offset).Trades.Should().ContainSingle().Which.Product.Should().Be(Product.Offset);
        simulation.Ledger.Available(simulation.FindCompany(1), Product.Offset).Should().Be(300m);
        simulation.Ledger.Available(simulation.FindCompany(2), Product.Offset).Should().Be(200m);
    }

    [Fact]
    public void Only_the_owner_can_cancel_an_order()
    {
        (Simulation simulation, Exchange exchange) = Build();
        Order sell = exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m, price: 105m));

        Action byStranger = () => exchange.Cancel(simulation.FindCompany(2), sell.Id);

        byStranger.Should().Throw<InvalidOperationException>().WithMessage("*belongs*");
        exchange.Book(Vintage1).OpenOrders.Should().ContainSingle();
    }

    [Fact]
    public void Order_ids_are_unique_across_books_so_a_cancel_finds_the_right_one()
    {
        (Simulation simulation, Exchange exchange) = Build();
        Product vintage2 = Product.Allowance(2);
        simulation.Ledger.Grant(simulation.FindCompany(1), vintage2, 500m);
        Company seller = simulation.FindCompany(1);

        Order first = exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m, price: 105m));
        Order second = exchange.Place(Order(
            simulation, 1, OrderSide.Sell, 100m, price: 105m, product: vintage2));

        second.Id.Should().NotBe(first.Id);
        exchange.Cancel(seller, second.Id).Should().BeSameAs(second);
        exchange.Book(Vintage1).OpenOrders.Should().ContainSingle().Which.Should().BeSameAs(first);
        exchange.Book(vintage2).OpenOrders.Should().BeEmpty();
        simulation.Ledger.Available(seller, vintage2).Should().Be(500m);
    }

    [Fact]
    public void A_stop_triggered_by_a_trade_cannot_steal_liquidity_from_the_order_being_matched()
    {
        (Simulation simulation, Exchange exchange) = Build();
        exchange.Place(Order(simulation, 1, OrderSide.Sell, 50m, price: 100m));
        exchange.Place(Order(simulation, 2, OrderSide.Sell, 100m, price: 105m));

        // A buy stop above the last trade waits for a trade to reach it.
        Order stop = exchange.Place(Order(
            simulation, 3, OrderSide.Buy, 100m, kind: OrderKind.StopLoss, stopPrice: 105m));
        stop.Status.Should().Be(OrderStatus.Open);

        Order buy = exchange.Place(Order(simulation, 4, OrderSide.Buy, 150m, kind: OrderKind.Market));

        buy.FilledVolume.Should().Be(150m, "the incoming order is matched before any stop reacts to the price");
        buy.Status.Should().Be(OrderStatus.Filled);
        stop.Status.Should().Be(OrderStatus.Cancelled, "the stop reacted afterwards and found nothing left");
        exchange.Book(Vintage1).Trades.Select(trade => trade.Price).Should().Equal(100m, 105m);
    }

    [Fact]
    public void A_stop_order_that_can_only_fill_partly_is_killed_when_it_asked_for_all_or_nothing()
    {
        (Simulation simulation, Exchange exchange) = Build();
        exchange.Place(Order(simulation, 4, OrderSide.Buy, 100m, price: 95m));
        Order stop = exchange.Place(Order(
            simulation, 1, OrderSide.Sell, 100m, kind: OrderKind.StopLoss, stopPrice: 95m, fillPolicy: FillPolicy.FillOrKill));

        // A small sale moves the price to the stop, leaving only 50 of the bid.
        exchange.Place(Order(simulation, 2, OrderSide.Sell, 50m, kind: OrderKind.Market));

        stop.FilledVolume.Should().Be(0m);
        stop.Status.Should().Be(OrderStatus.Cancelled);
        simulation.Ledger.Escrowed(simulation.FindCompany(1), Vintage1).Should().Be(0m, "a killed stop hands its escrow back");
    }

    [Fact]
    public void A_fill_or_kill_order_is_killed_when_the_band_moves_under_it()
    {
        (Simulation simulation, Exchange exchange) = Build();
        exchange.Place(Order(simulation, 1, OrderSide.Sell, 50m, price: 95m));
        exchange.Place(Order(simulation, 2, OrderSide.Sell, 50m, price: 105m));

        // Filling the 95 offer first would move the band to 85.5..104.5 and put the 105 offer out
        // of reach, so an all-or-nothing buyer must be killed rather than half filled.
        Order buy = exchange.Place(Order(
            simulation, 4, OrderSide.Buy, 100m, price: 110m, fillPolicy: FillPolicy.FillOrKill));

        buy.FilledVolume.Should().Be(0m);
        buy.Status.Should().Be(OrderStatus.Cancelled);
        exchange.Book(Vintage1).Trades.Should().BeEmpty();
        exchange.Book(Vintage1).LastTradePrice.Should().Be(100m);
        exchange.Book(Vintage1).OpenOrders.Should().HaveCount(2);
    }

    [Fact]
    public void A_fill_or_kill_order_never_rests_even_when_it_fills_part_of_the_way()
    {
        (Simulation simulation, Exchange exchange) = Build();
        exchange.Place(Order(simulation, 1, OrderSide.Sell, 100m, price: 105m));

        Order buy = exchange.Place(Order(
            simulation, 4, OrderSide.Buy, 100m, price: 105m, fillPolicy: FillPolicy.FillOrKill));

        buy.Status.Should().Be(OrderStatus.Filled);
        exchange.Book(Vintage1).OpenOrders.Should().BeEmpty();
    }
}
