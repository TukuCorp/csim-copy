namespace CarbonSim.Web.Accounts;

/// <summary>
/// Hands a message to whatever delivers it. The account code never talks to a mail transport
/// itself, so a test can read the password-reset code the way a person would.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
