namespace CarbonSim.Web.Contracts;

/// <summary>
/// What a visitor posts to claim a company. Every field may be missing on the wire, so the host
/// checks them instead of trusting the annotations.
/// </summary>
public sealed record RegisterAccountRequest
{
    public string? Email { get; init; }

    public string? DisplayName { get; init; }

    public string? Password { get; init; }

    /// <summary>One of the companies the roster offers.</summary>
    public string? Company { get; init; }

    /// <summary>The administrator access PIN, without which registration is refused.</summary>
    public string? RegistrationPin { get; init; }
}
