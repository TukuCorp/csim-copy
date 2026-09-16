namespace CarbonSim.Engine.Domain;

/// <summary>
/// A participant: a human trainer-controlled player or a rule-based AI player. Each player
/// owns the companies that name it as owner; the simulation wires that link at load time.
/// </summary>
public sealed class Player
{
    private readonly List<Company> _companies = [];

    public Player(int id, string name, PlayerKind kind)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A player needs a name.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        Kind = kind;
    }

    public int Id { get; }

    public string Name { get; }

    public PlayerKind Kind { get; }

    public IReadOnlyList<Company> Companies => _companies;

    internal void AddCompany(Company company) => _companies.Add(company);

    public override string ToString() => Name;
}
