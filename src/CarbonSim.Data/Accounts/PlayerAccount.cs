namespace CarbonSim.Data.Accounts;

/// <summary>
/// One login. The engine's <c>Player</c> is the in-game owner of a company; an account is the
/// person at the keyboard, and the two are joined by <see cref="CompanyName"/> when a human
/// company is claimed at registration. Nothing in the engine depends on this type.
/// </summary>
public sealed class PlayerAccount
{
    public int Id { get; set; }

    /// <summary>Stored normalised (trimmed, lower case), which is also the unique key.</summary>
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>A PBKDF2 hash produced by the host's password hasher; never a password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public AccountRole Role { get; set; }

    /// <summary>The human company this account plays, or null while it has not claimed one.</summary>
    public string? CompanyName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Hash of the code emailed for a password reset, or null when no reset is open.</summary>
    public string? ResetCodeHash { get; set; }

    public DateTimeOffset? ResetCodeExpiresAtUtc { get; set; }

    /// <summary>How many wrong codes this reset has already seen; the host sets the limit.</summary>
    public int ResetCodeAttempts { get; set; }
}
