namespace CarbonSim.Engine.Domain;

/// <summary>A sector of the economy whose units are covered by a trading system.</summary>
public sealed class Sector
{
    public Sector(
        string name,
        decimal emissionShare,
        IReadOnlyList<AbatementMenu>? abatementMenu = null,
        decimal? freeAllocationShare = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A sector needs a name.", nameof(name));
        }

        if (emissionShare is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(emissionShare),
                emissionShare,
                "A sector emission share is a fraction of the cap, so it must be between 0 and 1.");
        }

        if (freeAllocationShare is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(freeAllocationShare),
                freeAllocationShare,
                "A sector's free allocation share is a fraction of its slice of the cap, so it must be between 0 and 1.");
        }

        Name = name.Trim();
        EmissionShare = emissionShare;
        AbatementMenu = abatementMenu ?? [];
        FreeAllocationShare = freeAllocationShare;
    }

    public string Name { get; }

    /// <summary>Share of the trading system's cap this sector's units emit.</summary>
    public decimal EmissionShare { get; }

    /// <summary>
    /// The abatement projects every unit of this sector may buy, sized per unit through
    /// <see cref="AbatementMenu.For"/>.
    /// </summary>
    public IReadOnlyList<AbatementMenu> AbatementMenu { get; }

    /// <summary>
    /// The share of this sector's slice of the cap handed out free, where the exercise sets one per
    /// sector (catalog `FreeAllocationSectorAllocation`); null takes the trading system's share.
    /// </summary>
    public decimal? FreeAllocationShare { get; }

    public override string ToString() => Name;
}
