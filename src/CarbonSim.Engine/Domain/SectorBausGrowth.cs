namespace CarbonSim.Engine.Domain;

/// <summary>
/// The band a sector's business-as-usual emissions grow within, as an annual rate
/// (0.02 = +2%/yr). Each unit draws from its sector's band when a simulation starts.
/// </summary>
public sealed record SectorBausGrowth(string Sector, decimal MinAnnualRate, decimal MaxAnnualRate)
{
    public override string ToString() => $"{Sector}: {MinAnnualRate}..{MaxAnnualRate}";
}
