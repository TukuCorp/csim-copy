using System.Globalization;
using CarbonSim.Data.Accounts;
using CarbonSim.Web.Contracts;
using Microsoft.AspNetCore.Identity;

namespace CarbonSim.Web.Accounts;

/// <summary>
/// Everything a person does to their own account: register against a company that is waiting for
/// a player, sign in, and recover a forgotten password with an emailed code. The endpoints only
/// translate what happens here into status codes, so the rules live in one place.
/// </summary>
internal sealed class PlayerAccountService(
    IAccountStore store,
    ICompanyRoster roster,
    IEmailSender emailSender,
    IPasswordHasher<PlayerAccount> passwordHasher,
    CarbonSimHostOptions options,
    ILogger<PlayerAccountService> logger)
{
    /// <summary>Shortest password the host accepts, at registration and at a reset.</summary>
    private const int MinimumPasswordLength = 8;

    /// <summary>Deliberately says nothing about which half of the pair was wrong.</summary>
    private const string InvalidCredentialsMessage = "That address and password do not match an account.";

    private const string NoOpenResetMessage =
        "There is no password reset waiting for that address. Request a new code.";

    private const string TooManyAttemptsMessage =
        "That reset has seen too many wrong codes. Request a new one.";

    private readonly IAccountStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICompanyRoster _roster = roster ?? throw new ArgumentNullException(nameof(roster));
    private readonly IEmailSender _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    private readonly IPasswordHasher<PlayerAccount> _passwordHasher = passwordHasher
        ?? throw new ArgumentNullException(nameof(passwordHasher));
    private readonly CarbonSimHostOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<PlayerAccountService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Claims a company for a new player. The registration PIN is the gate, the roster decides
    /// which companies exist, and the account table's unique indexes settle the race when two
    /// visitors register at the same moment.
    /// </summary>
    public async Task<AccountOutcome> RegisterAsync(
        RegisterAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@', StringComparison.Ordinal))
        {
            return AccountOutcome.Refused(AccountProblem.InvalidRequest, "A valid email address is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return AccountOutcome.Refused(AccountProblem.InvalidRequest, "A display name is required.");
        }

        if (request.Password is not { Length: >= MinimumPasswordLength })
        {
            return AccountOutcome.Refused(
                AccountProblem.InvalidRequest,
                $"Choose a password of at least {MinimumPasswordLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Company))
        {
            return AccountOutcome.Refused(AccountProblem.InvalidRequest, "Choose one of the companies on offer.");
        }

        if (string.IsNullOrWhiteSpace(_options.RegistrationPin))
        {
            _logger.LogWarning("A visitor tried to register but this host has no registration PIN configured.");

            return AccountOutcome.Refused(
                AccountProblem.RegistrationClosed,
                "Registration is closed on this host: no administrator access PIN is configured.");
        }

        if (!FixedTimeSecret.Equals(request.RegistrationPin, _options.RegistrationPin))
        {
            return AccountOutcome.Refused(AccountProblem.WrongRegistrationPin, "The administrator access PIN is not correct.");
        }

        string company = request.Company.Trim();

        if (!await _roster.IsClaimableAsync(company, cancellationToken).ConfigureAwait(false))
        {
            return AccountOutcome.Refused(
                AccountProblem.CompanyUnavailable,
                $"'{company}' is not one of the companies waiting for a player.");
        }

        if (await _store.FindAsync(request.Email, cancellationToken).ConfigureAwait(false) is not null)
        {
            return AccountOutcome.Refused(AccountProblem.AddressOrCompanyTaken, "That address is already registered.");
        }

        // The hasher takes a user for context, and the values about to be stored are that context.
        PlayerAccount draft = new() { Email = request.Email.Trim(), DisplayName = request.DisplayName.Trim() };
        string passwordHash = _passwordHasher.HashPassword(draft, request.Password);

        PlayerAccount? created = await _store
            .CreateAsync(request.Email, request.DisplayName, passwordHash, AccountRole.Player, company, cancellationToken)
            .ConfigureAwait(false);

        if (created is null)
        {
            return AccountOutcome.Refused(
                AccountProblem.AddressOrCompanyTaken,
                "That address or company was taken while you were registering. Try again.");
        }

        _logger.LogInformation("Registered {Email} for company {Company}.", created.Email, created.CompanyName);

        return AccountOutcome.Ok(created);
    }

    /// <summary>
    /// Checks a password and, when the hasher has moved on to better parameters, stores the
    /// password again in the new form while the plain one is there to hash.
    /// </summary>
    public async Task<AccountOutcome> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AccountOutcome.Refused(AccountProblem.InvalidCredentials, InvalidCredentialsMessage);
        }

        PlayerAccount? account = await _store.FindAsync(request.Email, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            return AccountOutcome.Refused(AccountProblem.InvalidCredentials, InvalidCredentialsMessage);
        }

        PasswordVerificationResult verification =
            _passwordHasher.VerifyHashedPassword(account, account.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return AccountOutcome.Refused(AccountProblem.InvalidCredentials, InvalidCredentialsMessage);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            string rehashed = _passwordHasher.HashPassword(account, request.Password);
            await _store.SetPasswordAsync(account.Id, rehashed, cancellationToken).ConfigureAwait(false);
        }

        return AccountOutcome.Ok(account);
    }

    /// <summary>
    /// Opens a reset and emails the code. Whether or not the address has an account, the caller
    /// gets the same answer and the same silence; only a registered address is written to.
    /// </summary>
    public async Task RequestPasswordResetAsync(
        PasswordResetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return;
        }

        PlayerAccount? account = await _store.FindAsync(request.Email, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            _logger.LogInformation("A password reset was requested for an address that has no account.");
            return;
        }

        string code = PasswordResetCode.Generate();
        DateTimeOffset expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(_options.ResetCodeLifetimeMinutes);

        await _store
            .SetResetCodeAsync(account.Id, _passwordHasher.HashPassword(account, code), expiresAtUtc, cancellationToken)
            .ConfigureAwait(false);

        EmailMessage message = new(account.Email, ResetEmailSubject, ResetEmailBody(code, expiresAtUtc));

        try
        {
            await _emailSender.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The answer must not depend on whether the address exists, so a broken transport
            // cannot become a different status code here. The operator sees it in the log.
            _logger.LogError(exception, "The password reset code for {Email} could not be delivered.", account.Email);
        }
    }

    /// <summary>
    /// Turns an emailed code plus a new password into a password change. The code expires, works
    /// once, and stops working after a few wrong tries, all of which the account row records.
    /// </summary>
    public async Task<AccountOutcome> ConfirmPasswordResetAsync(
        ConfirmPasswordResetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code))
        {
            return AccountOutcome.Refused(AccountProblem.ResetCodeRejected, NoOpenResetMessage);
        }

        if (request.NewPassword is not { Length: >= MinimumPasswordLength })
        {
            return AccountOutcome.Refused(
                AccountProblem.InvalidRequest,
                $"Choose a password of at least {MinimumPasswordLength} characters.");
        }

        PlayerAccount? found = await _store.FindAsync(request.Email, cancellationToken).ConfigureAwait(false);

        if (found is null
            || found.ResetCodeHash is not string codeHash
            || found.ResetCodeExpiresAtUtc is not DateTimeOffset expiresAtUtc)
        {
            return AccountOutcome.Refused(AccountProblem.ResetCodeRejected, NoOpenResetMessage);
        }

        if (found.ResetCodeAttempts >= _options.ResetCodeMaxAttempts)
        {
            await _store.ClearResetCodeAsync(found.Id, cancellationToken).ConfigureAwait(false);
            return AccountOutcome.Refused(AccountProblem.ResetCodeRejected, TooManyAttemptsMessage);
        }

        if (expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            await _store.ClearResetCodeAsync(found.Id, cancellationToken).ConfigureAwait(false);
            return AccountOutcome.Refused(AccountProblem.ResetCodeRejected, "That code has expired. Request a new one.");
        }

        if (_passwordHasher.VerifyHashedPassword(found, codeHash, request.Code) == PasswordVerificationResult.Failed)
        {
            int attempts = await _store.RecordFailedResetAttemptAsync(found.Id, cancellationToken).ConfigureAwait(false);

            if (attempts >= _options.ResetCodeMaxAttempts)
            {
                await _store.ClearResetCodeAsync(found.Id, cancellationToken).ConfigureAwait(false);
                return AccountOutcome.Refused(AccountProblem.ResetCodeRejected, TooManyAttemptsMessage);
            }

            return AccountOutcome.Refused(AccountProblem.ResetCodeRejected, "That code is not correct.");
        }

        // Storing the new password also clears the reset, so the code cannot be used twice.
        string passwordHash = _passwordHasher.HashPassword(found, request.NewPassword);
        await _store.SetPasswordAsync(found.Id, passwordHash, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Reset the password of {Email}.", found.Email);

        return AccountOutcome.Ok(found);
    }

    private const string ResetEmailSubject = "Your CarbonSim password reset code";

    private static string ResetEmailBody(string code, DateTimeOffset expiresAtUtc)
    {
        string expires = expiresAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

        return $"""
            Someone asked to reset the password of this CarbonSim account.

            Your verification code is {code}. It works once and expires at {expires}.

            If this was not you, ignore this message: the password has not changed.
            """;
    }
}
