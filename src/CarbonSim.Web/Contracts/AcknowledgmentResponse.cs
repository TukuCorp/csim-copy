namespace CarbonSim.Web.Contracts;

/// <summary>
/// What the host replies when there is nothing more it may say - a password reset request, for
/// instance, which never tells the caller whether the address has an account.
/// </summary>
public sealed record AcknowledgmentResponse(string Message);
