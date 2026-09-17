using CarbonSim.Data.Accounts;
using Microsoft.EntityFrameworkCore;

namespace CarbonSim.Data.Accounts;

/// <summary>
/// The account store over the application database. Every method commits what it did before
/// returning, so a caller never holds a half-applied registration.
/// </summary>
public sealed class EfAccountStore(CarbonSimDbContext context) : IAccountStore
{
    private readonly CarbonSimDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<PlayerAccount?> FindAsync(string email, CancellationToken cancellationToken = default)
    {
        string normalized = Normalize(email);

        return await _context
            .Accounts.AsNoTracking()
            .SingleOrDefaultAsync(account => account.Email == normalized, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PlayerAccount?> FindByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context
            .Accounts.AsNoTracking()
            .SingleOrDefaultAsync(account => account.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PlayerAccount>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context
            .Accounts.AsNoTracking()
            .OrderBy(account => account.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> IsCompanyTakenAsync(string companyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);

        string trimmed = companyName.Trim();

        return await _context
            .Accounts.AsNoTracking()
            .AnyAsync(account => account.CompanyName == trimmed, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PlayerAccount?> CreateAsync(
        string email,
        string displayName,
        string passwordHash,
        AccountRole role,
        string? companyName = null,
        CancellationToken cancellationToken = default)
    {
        string normalized = Normalize(email);
        string? company = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();

        if (await _context.Accounts.AnyAsync(account => account.Email == normalized, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        if (company is not null
            && await _context.Accounts.AnyAsync(account => account.CompanyName == company, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        PlayerAccount created = new()
        {
            Email = normalized,
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            CompanyName = company,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _context.Accounts.Add(created);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return created;
    }

    public Task<bool> SetPasswordAsync(
        int accountId,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        return EditAsync(
            accountId,
            account =>
            {
                account.PasswordHash = passwordHash;
                account.ResetCodeHash = null;
                account.ResetCodeExpiresAtUtc = null;
                account.ResetCodeAttempts = 0;
            },
            cancellationToken);
    }

    public Task<bool> SetResetCodeAsync(
        int accountId,
        string codeHash,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        return EditAsync(
            accountId,
            account =>
            {
                account.ResetCodeHash = codeHash;
                account.ResetCodeExpiresAtUtc = expiresAtUtc;
                account.ResetCodeAttempts = 0;
            },
            cancellationToken);
    }

    public Task<bool> ClearResetCodeAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return EditAsync(
            accountId,
            account =>
            {
                account.ResetCodeHash = null;
                account.ResetCodeExpiresAtUtc = null;
                account.ResetCodeAttempts = 0;
            },
            cancellationToken);
    }

    public async Task<int> RecordFailedResetAttemptAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        PlayerAccount? account = await _context
            .Accounts.SingleOrDefaultAsync(candidate => candidate.Id == accountId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return 0;
        }

        account.ResetCodeAttempts++;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return account.ResetCodeAttempts;
    }

    private async Task<bool> EditAsync(
        int accountId,
        Action<PlayerAccount> edit,
        CancellationToken cancellationToken)
    {
        PlayerAccount? account = await _context
            .Accounts.SingleOrDefaultAsync(candidate => candidate.Id == accountId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return false;
        }

        edit(account);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>Addresses are stored and compared trimmed and lower case, in one place.</summary>
    private static string Normalize(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return email.Trim().ToLowerInvariant();
    }
}
