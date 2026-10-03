using System.Net;
using CarbonSim.Engine.Domain;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// The hub is for signed-in players only. A connection with no authentication cookie cannot even
/// negotiate, and a signed-in player can; the registry then keeps every action inside the caller's
/// own company and reports a refusal to the client with its reason intact.
/// </summary>
public sealed class HubAuthorizationTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public HubAuthorizationTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_open_the_hub()
    {
        using HttpClient client = _host.CreateClient();

        HttpResponseMessage response = await client.PostAsync("/hubs/simulation/negotiate?negotiateVersion=1", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_signed_in_player_can_open_the_hub()
    {
        SignedIn player = await _host.SignInAsync(TestRunFactory.DeltaCompany);

        using HttpClient client = _host.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", player.Cookie);

        HttpResponseMessage response = await client.PostAsync("/hubs/simulation/negotiate?negotiateVersion=1", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_rule_violation_reaches_the_client_with_its_message()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Guid id = runs.Start(simulation);
        await _host.CreateDriver().StartAsync(id);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);

        await using HubConnection connection = _host.CreateHubBuilder(delta.Cookie).Build();
        await connection.StartAsync();
        await connection.InvokeAsync("JoinSimulation", id);

        // Delta owns unit 1; bidding for Red River's unit 2 is refused, and the reason travels.
        Func<Task> bid = () => connection.InvokeAsync("PlaceAuctionBid", id, 2, 1, 140m, 1_000m);

        await bid.Should().ThrowAsync<HubException>().WithMessage("*does not play*");
    }
}
