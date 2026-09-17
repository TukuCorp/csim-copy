namespace CarbonSim.Web.Accounts;

/// <summary>One message the host wants delivered.</summary>
/// <param name="To">The recipient address, normalised the way the account store stores it.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="Body">The plain-text body.</param>
public sealed record EmailMessage(string To, string Subject, string Body);
