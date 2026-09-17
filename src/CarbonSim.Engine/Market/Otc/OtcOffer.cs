using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Market.Otc;

/// <summary>
/// A seller's offer to one named unit. Only the buyer it names can accept or reject it, only
/// the seller can withdraw it, and the instruments it promises are set aside the moment it is
/// sent, so they cannot be sold twice while the buyer makes up their mind.
/// </summary>
public sealed class OtcOffer
{
    internal OtcOffer(long id, Unit seller, Unit buyer, Product product, decimal price, decimal volume, int year)
    {
        Id = id;
        Seller = seller;
        Buyer = buyer;
        Product = product;
        Price = price;
        Volume = volume;
        Year = year;
        State = OtcOfferState.Pending;
    }

    public long Id { get; }

    /// <summary>The virtual year the offer was made in, stamped the way a trade's year is.</summary>
    public int Year { get; }

    public Unit Seller { get; }

    /// <summary>The unit the offer was addressed to; only its company may answer it.</summary>
    public Unit Buyer { get; }

    public Product Product { get; }

    public decimal Price { get; }

    public decimal Volume { get; }

    public OtcOfferState State { get; internal set; }

    public decimal Consideration => Price * Volume;

    public override string ToString() => $"{Seller.Name} offers {Volume} {Product} to {Buyer.Name} at {Price} ({State})";
}
