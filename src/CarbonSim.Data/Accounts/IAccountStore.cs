namespace CarbonSim.Data.Accounts;

/// <summary>
/// The few things the host needs from the account table, and nothing more. Addresses are
/// matched case-insensitively; a company can be claimed by one account only.
/// </summary>
public interface IAccountStore
{
    /// <summary>Finds the account with this address, or null when nobody has registered with it.</summary>
    Task<PlayerAccount?> FindAsync(string email, CancellationToken cancellationToken = default);

    Task<PlayerAccount?> FindByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Every account, in the order they registered.</summary>
    Task<IReadOnlyList<PlayerAccount>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether this human company has already been claimed by an account.</summary>
    Task<bool> IsCompanyTakenAsync(string companyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers an account, storing the address normalised. Returns null when the address is
    /// already registered or the company has been claimed in the meantime, so the host can tell
    /// the two apart with its own checks and treat null as the race.
    /// </summary>
    Task<PlayerAccount?> CreateAsync(
        string email,
        string displayName,
        string passwordHash,
        AccountRole role,
        string? companyName = null,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces the password hash. False when the account no longer exists.</summary>
    Task<bool> SetPasswordAsync(int accountId, string passwordHash, CancellationToken cancellationToken = default);

    /// <summary>Opens a password reset with the given code hash and expiry.</summary>
    Task<bool> SetResetCodeAsync(
        int accountId,
        string codeHash,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Closes any open reset, whether it was used, expired or abandoned.</summary>
    Task<bool> ClearResetCodeAsync(int accountId, CancellationToken cancellationToken = default);

    /// <summary>Counts a wrong code and returns how many this open reset has now seen.</summary>
    Task<int> RecordFailedResetAttemptAsync(int accountId, CancellationToken cancellationToken = default);
}
