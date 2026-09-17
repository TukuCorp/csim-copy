using CarbonSim.Web;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests;

/// <summary>
/// Proves the fixture, not the host: if the settings a fixture supplies did not reach the host,
/// every other test in this project would be checking the wrong database.
/// </summary>
public sealed class HostConfigurationTests : IClassFixture<CarbonSimWebHost>
{
    private readonly CarbonSimWebHost _host;

    public HostConfigurationTests(CarbonSimWebHost host)
    {
        _host = host;
    }

    [Fact]
    public void The_host_reads_its_settings_from_configuration()
    {
        CarbonSimHostOptions options = _host.Services.GetRequiredService<CarbonSimHostOptions>();

        options.RegistrationPin.Should().Be(_host.RegistrationPin);
        options.ConnectionString.Should().Contain(_host.DatabasePath);
        options.ResetCodeMaxAttempts.Should().Be(_host.ResetCodeMaxAttempts);
        options.EmailSender.Should().Be(CarbonSimHostOptions.InMemoryEmailSender);
        options.Schema.Should().Be(_host.SchemaStrategy);

        File.Exists(_host.DatabasePath).Should().BeTrue("the host creates the schema before it serves a request");
    }
}
