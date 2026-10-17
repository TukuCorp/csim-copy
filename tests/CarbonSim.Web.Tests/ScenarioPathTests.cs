using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace CarbonSim.Web.Tests;

/// <summary>
/// A published host is started from its publish folder, or from wherever an operator happens to be
/// standing, and either way its content root is not the source tree. The shipped scenario has to be
/// found from the app's own output, with no ScenarioFile setting, or registration offers nobody.
/// </summary>
public sealed class ScenarioPathTests : IClassFixture<ScenarioPathTests.ForeignContentRootHost>
{
    private readonly ForeignContentRootHost _host;

    public ScenarioPathTests(ForeignContentRootHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task The_default_scenario_is_found_when_the_content_root_holds_no_scenarios()
    {
        using HttpClient client = _host.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/accounts/companies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string[] offered = await response.Content.ReadFromJsonAsync<string[]>() ?? [];
        offered.Should().HaveCount(37, "the shipped scenario has 37 human companies");
    }

    /// <summary>The real host with its content root moved to an empty folder and no scenario setting.</summary>
    public sealed class ForeignContentRootHost : CarbonSimWebHost
    {
        private readonly string _contentRoot = Directory.CreateTempSubdirectory("carbonsim-content-root-").FullName;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseContentRoot(_contentRoot);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                try
                {
                    Directory.Delete(_contentRoot, recursive: true);
                }
                catch (IOException)
                {
                    // An empty folder left in temp is a tidiness problem, not a test failure.
                }
            }
        }
    }
}
