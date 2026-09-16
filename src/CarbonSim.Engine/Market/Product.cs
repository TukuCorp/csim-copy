namespace CarbonSim.Engine.Market;

/// <summary>What is being traded: a vintage-dated allowance, or an offset that carries no vintage.</summary>
public enum ProductKind
{
    Allowance,
    Offset,
}

public readonly record struct Product(ProductKind Kind, int Vintage)
{
    /// <summary>An allowance of a given vintage year.</summary>
    public static Product Allowance(int vintage) => new(ProductKind.Allowance, vintage);

    /// <summary>An offset: usable against a share of any year's obligation, but never vintage-dated.</summary>
    public static Product Offset => new(ProductKind.Offset, 0);

    public bool IsOffset => Kind == ProductKind.Offset;

    public override string ToString() => IsOffset ? "Offset" : $"Vintage {Vintage}";
}
