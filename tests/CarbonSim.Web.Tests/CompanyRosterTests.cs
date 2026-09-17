using System.Net;
using System.Net.Http.Json;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Scenarios;
using FluentAssertions;

namespace CarbonSim.Web.Tests;

/// <summary>
/// The roster the host offers when nobody replaced it: the human companies of the scenario named by
/// configuration, which is the only roster a deployment runs.
/// </summary>
public sealed class CompanyRosterTests : IClassFixture<DefaultRosterHost>
{
    private readonly DefaultRosterHost _host;

    public CompanyRosterTests(DefaultRosterHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task The_default_roster_offers_the_scenarios_human_companies()
    {
        using HttpClient client = _host.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/accounts/companies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string[] offered = await response.Content.ReadFromJsonAsync<string[]>() ?? [];
        Simulation scenario = ScenarioLoader.LoadFile(RepositoryScenario.File);
        string[] human = [.. scenario.Companies.Where(company => company.Owner.Kind == PlayerKind.Human).Select(company => company.Name)];
        HashSet<string> claimed = [.. (await _host.ListAccountsAsync()).Select(account => account.CompanyName).OfType<string>()];

        human.Should().NotBeEmpty();
        offered.Should().Equal(
            human.Where(name => !claimed.Contains(name)),
            "the roster is exactly the companies a human owns and nobody has claimed, in scenario order");
    }

    [Fact]
    public async Task A_claimed_company_is_no_longer_offered()
    {
        using HttpClient client = _host.CreateClient();
        string[] before = await OfferedAsync(client);
        string claimed = before[0];

        (await client.RegisterAsync("first-claim@example.com", claimed, _host.RegistrationPin)).StatusCode
            .Should()
            .Be(HttpStatusCode.Created);

        (await OfferedAsync(client)).Should().Equal(before[1..], "a claimed company is off the market");

        using HttpClient latecomer = _host.CreateClient();
        (await latecomer.RegisterAsync("second-claim@example.com", claimed, _host.RegistrationPin)).StatusCode
            .Should()
            .Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_company_no_human_player_owns_cannot_be_claimed()
    {
        using HttpClient client = _host.CreateClient();
        Simulation scenario = ScenarioLoader.LoadFile(RepositoryScenario.File);
        string automated = scenario.Companies.First(company => company.Owner.Kind == PlayerKind.Ai).Name;

        HttpResponseMessage response = await client.RegisterAsync("wants-a-bot@example.com", automated, _host.RegistrationPin);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _host.FindAccountAsync("wants-a-bot@example.com")).Should().BeNull();
    }

    private static async Task<string[]> OfferedAsync(HttpClient client)
    {
        return await client.GetFromJsonAsync<string[]>("/api/accounts/companies") ?? [];
    }
}

/// <summary>The host as it ships: no roster override, no scenario override, the default scenario.</summary>
public sealed class DefaultRosterHost : CarbonSimWebHost;

/// <summary>Finds the shipped scenario the way a developer's checkout holds it.</summary>
internal static class RepositoryScenario
{
    public static string File { get; } = Locate();

    private static string Locate()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "scenarios", "vietnam-2024.json");

            if (System.IO.File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"The scenario file was not found above '{AppContext.BaseDirectory}'.");
    }
}
