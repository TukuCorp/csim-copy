namespace CarbonSim.Web.Accounts;

/// <summary>
/// Keeps what it was asked to send, in order, so a test can read a password-reset code the way its
/// owner would. Nothing leaves the process; this sender is for tests and for the development host.
/// </summary>
public sealed class InMemoryEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];
    private readonly Lock _sync = new();

    /// <summary>Everything sent so far, oldest first.</summary>
    public IReadOnlyList<EmailMessage> SentMessages
    {
        get
        {
            lock (_sync)
            {
                return [.. _sent];
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_sync)
        {
            _sent.Add(message);
        }

        return Task.CompletedTask;
    }

    /// <summary>Forgets everything sent so far, for a test that wants to look at one message only.</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _sent.Clear();
        }
    }
}
