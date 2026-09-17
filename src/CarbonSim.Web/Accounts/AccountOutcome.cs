using CarbonSim.Data.Accounts;

namespace CarbonSim.Web.Accounts;

/// <summary>What stopped an account operation from doing what was asked.</summary>
internal enum AccountProblem
{
    None,

    /// <summary>The request itself was unusable: missing fields, too short a password.</summary>
    InvalidRequest,

    /// <summary>The host has no registration PIN configured, so nobody may register.</summary>
    RegistrationClosed,

    /// <summary>The administrator access PIN did not match.</summary>
    WrongRegistrationPin,

    /// <summary>The chosen company is not one a player may claim.</summary>
    CompanyUnavailable,

    /// <summary>The address is registered already, or the company was claimed first.</summary>
    AddressOrCompanyTaken,

    /// <summary>The address and password do not match an account.</summary>
    InvalidCredentials,

    /// <summary>The reset code is wrong, expired, used up or already spent.</summary>
    ResetCodeRejected,
}

/// <summary>
/// The result of an operation on somebody's own account. A successful outcome always carries the
/// account it acted on. A refusal carries the message the caller is shown, which stays vague
/// whenever being precise would tell a stranger whether an address is registered.
/// </summary>
internal sealed record AccountOutcome(AccountProblem Problem, string Message, PlayerAccount? Account = null)
{
    public bool Succeeded => Problem == AccountProblem.None;

    public static AccountOutcome Ok(PlayerAccount account) => new(AccountProblem.None, string.Empty, account);

    public static AccountOutcome Refused(AccountProblem problem, string message) => new(problem, message);
}
