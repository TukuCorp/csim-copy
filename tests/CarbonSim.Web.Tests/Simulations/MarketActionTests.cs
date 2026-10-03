using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Reporting;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>Exchange and OTC actions through the registry: fills, books, offers and chat.</summary>
public sealed class MarketActionTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public MarketActionTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task A_crossing_order_fills_and_empties_the_book()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Guid id = runs.Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SignedIn redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);
        await _host.CreateDriver().StartAsync(id);

        Unit seller = simulation.FindUnit(1);
        Unit buyer = simulation.FindUnit(2);
        simulation.Ledger.Grant(seller.Company, Product.Allowance(1), 1_000m);

        await runs.PlaceOrderAsync(id, delta.Actor, 1, "Vintage 1", "Sell", "Limit", 100m, 110m);
        await runs.PlaceOrderAsync(id, redRiver.Actor, 2, "Vintage 1", "Buy", "Limit", 100m, 110m);

        simulation.Journal.Trades.Should().ContainSingle(trade => trade.Channel == TradeChannel.Exchange);
        MarketTrade fill = simulation.Journal.Trades.Single(trade => trade.Channel == TradeChannel.Exchange);
        fill.Price.Should().Be(110m);
        fill.Volume.Should().Be(100m);
        fill.Buyer.Should().Be(buyer.Company);
        fill.Seller.Should().Be(seller.Company);

        runs.Run(id).Exchange.Book(Product.Allowance(1)).OpenOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task An_otc_offer_accepted_moves_stock_and_cash()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Guid id = runs.Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SignedIn redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);
        await _host.CreateDriver().StartAsync(id);

        Unit seller = simulation.FindUnit(1);
        Unit buyer = simulation.FindUnit(2);
        decimal sellerFree = simulation.Ledger.Available(seller.Company, Product.Allowance(1));
        decimal buyerFree = simulation.Ledger.Available(buyer.Company, Product.Allowance(1));
        simulation.Ledger.Grant(seller.Company, Product.Allowance(1), 1_000m);
        decimal sellerBefore = seller.Company.Capital;
        decimal buyerBefore = buyer.Company.Capital;

        long offer = await runs.SendOtcOfferAsync(id, delta.Actor, 1, 2, "Vintage 1", 110m, 100m);
        await runs.AnswerOtcOfferAsync(id, redRiver.Actor, 2, offer, true);

        simulation.Ledger.Available(seller.Company, Product.Allowance(1)).Should().Be(sellerFree + 900m);
        simulation.Ledger.Available(buyer.Company, Product.Allowance(1)).Should().Be(buyerFree + 100m);
        buyer.Company.Capital.Should().BeLessThan(buyerBefore);
        seller.Company.Capital.Should().BeGreaterThan(sellerBefore);
        runs.Run(id).Otc.Offers.Single().State.Should().Be(CarbonSim.Engine.Market.Otc.OtcOfferState.Accepted);
    }
}
