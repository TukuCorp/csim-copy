namespace CarbonSim.Web.Contracts;

/// <summary>What a visitor posts to sign in.</summary>
public sealed record LoginRequest
{
    public string? Email { get; init; }

    public string? Password { get; init; }
}
