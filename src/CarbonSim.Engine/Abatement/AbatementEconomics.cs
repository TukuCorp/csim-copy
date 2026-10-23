namespace CarbonSim.Engine.Abatement;

/// <summary>
/// An abatement option's economics over a stretch of time: the years it operates, the tonnes it
/// removes, what a tonne costs once its running revenue is counted (null when it removes nothing),
/// and its return on the capital (null when it needs none).
/// </summary>
public sealed record AbatementEconomics(
    decimal OperatingYears,
    decimal Tonnes,
    decimal? NetCostPerTonne,
    decimal? ForecastRoi);
