namespace CarbonSim.Engine.Domain;

/// <summary>
/// One emissions trading system inside a simulation: the sectors it covers, the companies
/// that must comply, and the parameters the administrator runs it under.
/// </summary>
public sealed class TradingSystem
{
    public TradingSystem(
        int id,
        string name,
        Parameters parameters,
        IReadOnlyList<Sector> sectors,
        IReadOnlyList<Company> companies)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A trading system needs a name.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(sectors);
        ArgumentNullException.ThrowIfNull(companies);

        IReadOnlyList<string> parameterErrors = parameters.Validate();

        if (parameterErrors.Count > 0)
        {
            throw new ArgumentException(
                $"Trading system '{name}' cannot run on invalid parameters: {string.Join(" ", parameterErrors)}",
                nameof(parameters));
        }

        Id = id;
        Name = name.Trim();
        Parameters = parameters;
        Sectors = sectors;
        Companies = companies;
        Units = [.. companies.SelectMany(company => company.Units)];
    }

    public int Id { get; }

    public string Name { get; }

    public Parameters Parameters { get; }

    public IReadOnlyList<Sector> Sectors { get; }

    public IReadOnlyList<Company> Companies { get; }

    public IReadOnlyList<Unit> Units { get; }

    public override string ToString() => Name;
}
