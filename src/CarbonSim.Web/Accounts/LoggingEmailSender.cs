namespace CarbonSim.Web.Accounts;

/// <summary>
/// The sender a host runs when no mail transport is configured. It writes the whole message, code
/// included, to the log at warning level: a password-reset code that only ever existed in memory
/// would be lost for good, and the operator has to see that this host cannot deliver mail.
/// </summary>
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogWarning(
            "No email transport is configured; the message for {Recipient} is logged instead:\n{Subject}\n{Body}",
            message.To,
            message.Subject,
            message.Body);

        return Task.CompletedTask;
    }
}
