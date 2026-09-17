using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Snapshot;

namespace CarbonSim.Engine.Market.Otc;

/// <summary>
/// The over-the-counter market. Only sell-side offers exist, as in the original: a seller picks
/// a unit to offer allowances or offsets to, the buyer accepts or rejects, and either side can
/// walk away while the offer is pending. Instruments travel through the same escrow as the
/// order book, so an offer cannot promise what a resting order already holds.
/// </summary>
public sealed class OtcMarket
{
    private readonly Simulation _simulation;
    private readonly List<OtcOffer> _offers = [];

    public OtcMarket(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        _simulation = simulation;
    }

    /// <summary>
    /// Puts back every offer in the order it was made, with the state it was left in. An offer
    /// that is still pending is still holding its seller's escrow, and one that was accepted is
    /// part of the run's history, so both are put back rather than dropped.
    /// </summary>
    internal void Restore(OtcMarketSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _offers.Clear();

        foreach (OtcOfferSnapshot offer in snapshot.Offers)
        {
            _offers.Add(new OtcOffer(
                offer.Id,
                _simulation.FindUnit(offer.SellerUnitId),
                _simulation.FindUnit(offer.BuyerUnitId),
                offer.Product.ToProduct(),
                offer.Price,
                offer.Volume,
                offer.Year)
            {
                State = offer.State,
            });
        }
    }

    /// <summary>Every offer ever made, in the order it was made.</summary>
    public IReadOnlyList<OtcOffer> Offers => _offers;

    /// <summary>Offers still waiting for an answer.</summary>
    public IReadOnlyList<OtcOffer> Pending => [.. _offers.Where(offer => offer.State == OtcOfferState.Pending)];

    /// <summary>The pending offers a company is a party to, either as seller or as buyer.</summary>
    public IReadOnlyList<OtcOffer> PendingFor(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);

        return
        [
            .. Pending.Where(offer =>
                ReferenceEquals(offer.Seller.Company, company) || ReferenceEquals(offer.Buyer.Company, company))
        ];
    }

    /// <summary>Sends an offer to a named unit, setting the offered instruments aside.</summary>
    public OtcOffer Send(Unit seller, Unit buyer, Product product, decimal price, decimal volume)
    {
        RequireUnit(seller, nameof(seller), out Company sellerCompany);
        RequireUnit(buyer, nameof(buyer), out _);

        if (ReferenceEquals(seller, buyer))
        {
            throw new ArgumentException($"Unit '{seller.Name}' cannot make an offer to itself.", nameof(buyer));
        }

        if (price <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, "An offer price must be greater than zero.");
        }

        if (volume <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), volume, "An offer volume must be greater than zero.");
        }

        _simulation.Ledger.Escrow(sellerCompany, product, volume);

        OtcOffer offer = new(_offers.Count + 1, seller, buyer, product, price, volume, _simulation.CurrentYear);
        _offers.Add(offer);

        return offer;
    }

    /// <summary>The named buyer takes the offer: instruments and money change hands.</summary>
    public OtcOffer Accept(Unit buyer, long offerId)
    {
        OtcOffer offer = Answer(buyer, offerId);

        _simulation.Cash.Withdraw(
            offer.Buyer.Company,
            offer.Consideration,
            CashCategory.OtcPurchase,
            $"{offer.Volume} {offer.Product} from {offer.Seller.Name}");
        _simulation.Cash.Deposit(
            offer.Seller.Company,
            offer.Consideration,
            CashCategory.InstrumentSale,
            $"{offer.Volume} {offer.Product} to {offer.Buyer.Name}");
        _simulation.Ledger.SettleEscrow(offer.Seller.Company, offer.Buyer.Company, offer.Product, offer.Volume);
        _simulation.Journal.Record(
            TradeChannel.Otc,
            offer.Product,
            offer.Price,
            offer.Volume,
            offer.Buyer.Company,
            offer.Seller.Company);
        offer.State = OtcOfferState.Accepted;

        return offer;
    }

    /// <summary>The named buyer turns the offer down; the instruments go back to the seller.</summary>
    public OtcOffer Reject(Unit buyer, long offerId)
    {
        OtcOffer offer = Answer(buyer, offerId);

        _simulation.Ledger.ReleaseEscrow(offer.Seller.Company, offer.Product, offer.Volume);
        offer.State = OtcOfferState.Rejected;

        return offer;
    }

    /// <summary>The seller withdraws the offer before it is answered.</summary>
    public OtcOffer Cancel(Unit seller, long offerId)
    {
        OtcOffer offer = Find(offerId);

        if (!ReferenceEquals(offer.Seller, seller))
        {
            throw new InvalidOperationException(
                $"Offer {offerId} was made by '{offer.Seller.Name}'; only the seller can withdraw it.");
        }

        RequirePending(offer);

        _simulation.Ledger.ReleaseEscrow(offer.Seller.Company, offer.Product, offer.Volume);
        offer.State = OtcOfferState.Cancelled;

        return offer;
    }

    private OtcOffer Answer(Unit buyer, long offerId)
    {
        OtcOffer offer = Find(offerId);

        if (!ReferenceEquals(offer.Buyer, buyer))
        {
            throw new InvalidOperationException(
                $"Offer {offerId} was sent to '{offer.Buyer.Name}'; only the named buyer can answer it.");
        }

        RequirePending(offer);

        return offer;
    }

    private OtcOffer Find(long offerId)
    {
        return _offers.FirstOrDefault(offer => offer.Id == offerId)
            ?? throw new KeyNotFoundException($"This market has no offer {offerId}.");
    }

    private static void RequirePending(OtcOffer offer)
    {
        if (offer.State != OtcOfferState.Pending)
        {
            throw new InvalidOperationException($"Offer {offer.Id} has already been {offer.State.ToString().ToLowerInvariant()}.");
        }
    }

    private void RequireUnit(Unit unit, string parameterName, out Company company)
    {
        ArgumentNullException.ThrowIfNull(unit, parameterName);

        company = unit.Company;

        if (company.Units.All(candidate => !ReferenceEquals(candidate, unit)))
        {
            throw new ArgumentException($"Unit '{unit.Name}' is not part of this simulation.", parameterName);
        }
    }
}
