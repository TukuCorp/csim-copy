using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Otc;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Market;

public sealed class OtcMarketTests
{
    private static readonly Product Vintage1 = Product.Allowance(1);

    private static (Simulation Simulation, OtcMarket Otc) Build()
    {
        Simulation simulation = TestSimulation.Build();
        simulation.Ledger.Grant(simulation.FindCompany(1), Vintage1, 1_000_000m);
        simulation.Ledger.Grant(simulation.FindCompany(2), Product.Offset, 1_000_000m);

        return (simulation, new OtcMarket(simulation));
    }

    [Fact]
    public void An_offer_escrows_the_sellers_allowances_until_the_named_buyer_answers()
    {
        (Simulation simulation, OtcMarket otc) = Build();
        Company seller = simulation.FindCompany(1);

        OtcOffer offer = otc.Send(simulation.FindUnit(1), simulation.FindUnit(4), Vintage1, price: 105m, volume: 200m);

        offer.State.Should().Be(OtcOfferState.Pending);
        offer.Seller.Should().BeSameAs(simulation.FindUnit(1));
        offer.Buyer.Should().BeSameAs(simulation.FindUnit(4));
        simulation.Ledger.Escrowed(seller, Vintage1).Should().Be(200m);
        simulation.Ledger.Available(seller, Vintage1).Should().Be(999_800m);
        otc.Pending.Should().ContainSingle().Which.Should().BeSameAs(offer);
    }

    [Fact]
    public void Accepting_moves_the_instruments_and_the_money()
    {
        (Simulation simulation, OtcMarket otc) = Build();
        Company seller = simulation.FindCompany(1);
        Company buyer = simulation.FindCompany(2);
        decimal sellerCapital = seller.Capital;
        decimal buyerCapital = buyer.Capital;
        OtcOffer offer = otc.Send(simulation.FindUnit(1), simulation.FindUnit(4), Vintage1, price: 105m, volume: 200m);

        otc.Accept(simulation.FindUnit(4), offer.Id);

        offer.State.Should().Be(OtcOfferState.Accepted);
        simulation.Ledger.Escrowed(seller, Vintage1).Should().Be(0m);
        simulation.Ledger.Available(buyer, Vintage1).Should().Be(200m);
        seller.Capital.Should().Be(sellerCapital + (200m * 105m));
        buyer.Capital.Should().Be(buyerCapital - (200m * 105m));
        otc.Pending.Should().BeEmpty();
    }

    [Fact]
    public void Only_the_named_buyer_can_accept_or_reject()
    {
        (Simulation simulation, OtcMarket otc) = Build();
        OtcOffer offer = otc.Send(simulation.FindUnit(1), simulation.FindUnit(4), Vintage1, price: 105m, volume: 200m);

        Action strangerAccepts = () => otc.Accept(simulation.FindUnit(3), offer.Id);
        Action strangerRejects = () => otc.Reject(simulation.FindUnit(3), offer.Id);

        strangerAccepts.Should().Throw<InvalidOperationException>().WithMessage("*named*");
        strangerRejects.Should().Throw<InvalidOperationException>().WithMessage("*named*");
        offer.State.Should().Be(OtcOfferState.Pending);
        simulation.Ledger.Escrowed(simulation.FindCompany(1), Vintage1).Should().Be(200m);
    }

    [Fact]
    public void Rejecting_hands_the_escrowed_allowances_back()
    {
        (Simulation simulation, OtcMarket otc) = Build();
        Company seller = simulation.FindCompany(1);
        OtcOffer offer = otc.Send(simulation.FindUnit(1), simulation.FindUnit(4), Vintage1, price: 105m, volume: 200m);

        otc.Reject(simulation.FindUnit(4), offer.Id);

        offer.State.Should().Be(OtcOfferState.Rejected);
        simulation.Ledger.Escrowed(seller, Vintage1).Should().Be(0m);
        simulation.Ledger.Available(seller, Vintage1).Should().Be(1_000_000m);
    }

    [Fact]
    public void Only_the_seller_can_withdraw_an_offer()
    {
        (Simulation simulation, OtcMarket otc) = Build();
        OtcOffer offer = otc.Send(simulation.FindUnit(1), simulation.FindUnit(4), Vintage1, price: 105m, volume: 200m);

        Action stranger = () => otc.Cancel(simulation.FindUnit(4), offer.Id);
        stranger.Should().Throw<InvalidOperationException>().WithMessage("*seller*");

        otc.Cancel(simulation.FindUnit(1), offer.Id);

        offer.State.Should().Be(OtcOfferState.Cancelled);
        simulation.Ledger.Escrowed(simulation.FindCompany(1), Vintage1).Should().Be(0m);
    }

    [Fact]
    public void An_offer_can_only_be_answered_once()
    {
        (Simulation simulation, OtcMarket otc) = Build();
        OtcOffer offer = otc.Send(simulation.FindUnit(1), simulation.FindUnit(4), Vintage1, price: 105m, volume: 200m);
        otc.Accept(simulation.FindUnit(4), offer.Id);

        Action acceptAgain = () => otc.Accept(simulation.FindUnit(4), offer.Id);
        Action rejectAfterAccept = () => otc.Reject(simulation.FindUnit(4), offer.Id);
        Action cancelAfterAccept = () => otc.Cancel(simulation.FindUnit(1), offer.Id);

        acceptAgain.Should().Throw<InvalidOperationException>().WithMessage("*accepted*");
        rejectAfterAccept.Should().Throw<InvalidOperationException>().WithMessage("*accepted*");
        cancelAfterAccept.Should().Throw<InvalidOperationException>().WithMessage("*accepted*");
    }

    [Fact]
    public void A_seller_cannot_offer_instruments_it_does_not_hold()
    {
        (Simulation simulation, OtcMarket otc) = Build();

        Action tooMuch = () => otc.Send(simulation.FindUnit(1), simulation.FindUnit(4), Vintage1, price: 105m, volume: 2_000_000m);

        tooMuch.Should().Throw<InvalidOperationException>().WithMessage("*insufficient*");
        otc.Offers.Should().BeEmpty();
    }

    [Fact]
    public void Offers_can_be_made_for_offsets()
    {
        (Simulation simulation, OtcMarket otc) = Build();
        Company buyer = simulation.FindCompany(1);

        OtcOffer offer = otc.Send(simulation.FindUnit(4), simulation.FindUnit(1), Product.Offset, price: 80m, volume: 300m);
        otc.Accept(simulation.FindUnit(1), offer.Id);

        simulation.Ledger.Available(buyer, Product.Offset).Should().Be(300m);
        simulation.Ledger.Available(simulation.FindCompany(2), Product.Offset).Should().Be(999_700m);
    }

    [Fact]
    public void An_offer_needs_a_positive_price_and_volume_and_another_unit_to_sell_to()
    {
        (Simulation simulation, OtcMarket otc) = Build();

        Action noPrice = () => otc.Send(simulation.FindUnit(1), simulation.FindUnit(4), Vintage1, price: 0m, volume: 100m);
        Action noVolume = () => otc.Send(simulation.FindUnit(1), simulation.FindUnit(4), Vintage1, price: 105m, volume: 0m);
        Action toItself = () => otc.Send(simulation.FindUnit(1), simulation.FindUnit(1), Vintage1, price: 105m, volume: 100m);
        Action unknownOffer = () => otc.Accept(simulation.FindUnit(4), 99L);

        noPrice.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*price*");
        noVolume.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*volume*");
        toItself.Should().Throw<ArgumentException>().WithMessage("*itself*");
        unknownOffer.Should().Throw<KeyNotFoundException>().WithMessage("*99*");
    }

    [Fact]
    public void Both_sides_of_an_offer_can_see_it_while_it_is_pending()
    {
        (Simulation simulation, OtcMarket otc) = Build();
        OtcOffer offer = otc.Send(simulation.FindUnit(1), simulation.FindUnit(4), Vintage1, price: 105m, volume: 200m);

        otc.PendingFor(simulation.FindCompany(1)).Should().ContainSingle().Which.Should().BeSameAs(offer);
        otc.PendingFor(simulation.FindCompany(2)).Should().ContainSingle().Which.Should().BeSameAs(offer);
        otc.PendingFor(simulation.FindCompany(3)).Should().BeEmpty();
    }
}
