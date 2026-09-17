namespace CarbonSim.Web.Contracts;

/// <summary>
/// What a visitor posts to be sent a password-reset code. The answer never says whether the
/// address has an account.
/// </summary>
public sealed record PasswordResetRequest
{
    public string? Email { get; init; }
}
