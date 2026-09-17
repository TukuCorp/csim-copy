using System.Net;
using System.Text.RegularExpressions;
using CarbonSim.Web;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CarbonSim.Web.Tests;

/// <summary>
/// What the host does when it has no mail transport: it writes the whole message, reset code
/// included, to the log at warning level, so nothing is dropped without a trace.
/// </summary>
public sealed class EmailDeliveryTests : IClassFixture<LoggingEmailHost>
{
    private readonly LoggingEmailHost _host;

    public EmailDeliveryTests(LoggingEmailHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task The_default_sender_logs_the_reset_code_instead_of_dropping_it()
    {
        using HttpClient client = _host.CreateClient();
        (await client.RegisterAsync("no-transport@example.com", FakeRosterHost.PowerCompany, _host.RegistrationPin))
            .StatusCode
            .Should()
            .Be(HttpStatusCode.Created);

        HttpResponseMessage response = await client.RequestPasswordResetAsync("no-transport@example.com");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        string warning = _host.Captured.Messages
            .SingleOrDefault(message => message.Contains("No email transport is configured", StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Nothing warned about the missing transport. Logged: {string.Join(" | ", _host.Captured.Messages)}");

        warning.Should().Contain("no-transport@example.com");

        string code = Regex.Match(warning, @"\b(\d{6})\b").Groups[1].Value;
        code.Should().NotBeEmpty("the logged message carries the code the player needs");

        // The code from the log is the real one, so nothing about the message was lost on the way out.
        (await client.ConfirmPasswordResetAsync("no-transport@example.com", code)).StatusCode
            .Should()
            .Be(HttpStatusCode.NoContent);
    }
}

/// <summary>A host with no mail transport: the configuration names the logging sender, not the recording one.</summary>
public sealed class LoggingEmailHost : FakeRosterHost
{
    public CapturingLoggerProvider Captured { get; } = new();

    public override string EmailSenderName => CarbonSimHostOptions.LogEmailSender;

    protected override void ConfigureHostServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        base.ConfigureHostServices(services);

        services.AddLogging(logging => logging.AddProvider(Captured));
    }
}
