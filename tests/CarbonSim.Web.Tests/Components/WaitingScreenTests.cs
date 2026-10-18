using Bunit;
using CarbonSim.Web.Components.Pages;
using CarbonSim.Web.Player;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The public front door: the exercises on offer, who is looking at them, and the way on from
/// here. A guest is invited to sign in, a player is sent to their own company, and a trainer is
/// sent to the console.
/// </summary>
public sealed class WaitingScreenTests : ScreenTestContext
{
    private static readonly Guid RunId = Guid.Parse("9a7c1f4e-2d3b-4c5a-8e6f-1b0d2c3a4e5f");

    public WaitingScreenTests()
    {
        Session.Runs = [Summary()];
    }

    [Fact]
    public void The_waiting_screen_counts_the_companies_people_play_apart_from_the_ones_the_bots_run()
    {
        IRenderedComponent<Waiting> page = RenderComponent<Waiting>();
        string human = Localizer(Services)["HumanCompanies"].Value;
        string bots = Localizer(Services)["AiCompanies"].Value;

        page.Markup.Should().Contain($"{human}</dt><dd>37</dd>", "37 of the 242 companies are for people");
        page.Markup.Should().Contain($"{bots}</dt><dd>205</dd>", "the rest trade on their own");
        page.Markup.Should().Contain("242", "the exercise defines 242 companies and 242 units");
    }

    [Fact]
    public void A_signed_in_trainer_is_told_who_they_are_and_pointed_at_the_console()
    {
        Session.FrontDoor = new FrontDoorView("Tung", null, IsAdministrator: true, null);

        IRenderedComponent<Waiting> page = RenderComponent<Waiting>();

        page.Markup.Should().Contain(Localizer(Services)["SignedInAs", "Tung"].Value);
        page.FindAll("a").Should().Contain(link => link.GetAttribute("href") == "/admin");
        page.FindAll("a").Should().NotContain(link => link.GetAttribute("href") == "/sign-in");
    }

    [Fact]
    public void A_signed_in_player_is_pointed_at_their_own_run()
    {
        Session.FrontDoor = new FrontDoorView("Lan", "Delta Power", IsAdministrator: false, RunId);

        IRenderedComponent<Waiting> page = RenderComponent<Waiting>();

        page.Markup.Should().Contain(Localizer(Services)["SignedInAs", "Lan"].Value);
        page.FindAll("a").Should().Contain(link => link.GetAttribute("href") == $"/run/{RunId:D}/dashboard");
        page.FindAll("a").Should().NotContain(link => link.GetAttribute("href") == "/sign-in");
    }

    [Fact]
    public void A_guest_is_offered_the_sign_in_page()
    {
        IRenderedComponent<Waiting> page = RenderComponent<Waiting>();

        page.FindAll("a").Should().Contain(link => link.GetAttribute("href") == "/sign-in");
        page.Markup.Should().NotContain("Signed in as");
    }

    [Fact]
    public void The_heading_turns_over_once_an_exercise_is_under_way()
    {
        Session.Runs = [Summary() with { State = "Running", Year = 2 }];

        IRenderedComponent<Waiting> page = RenderComponent<Waiting>();

        page.Find("h1").TextContent.Should().Contain(Localizer(Services)["SimulationInProgress"].Value);
        page.Find("h1").TextContent.Should().NotContain(Localizer(Services)["WaitingForSimulationToStart"].Value);
        page.Markup.Should().Contain("Running", "the run's own state is shown");
        page.Markup.Should().Contain("Year 2");
        page.FindAll("a").Should().Contain(link => link.TextContent.Trim() == Localizer(Services)["OpenSimulation"].Value);
    }

    internal static RunSummary Summary() => new(
        RunId,
        "Vietnam ETS Pilot 2024",
        "Pending",
        1,
        3,
        242,
        37,
        242,
        4,
        TimeSpan.FromMinutes(20),
        355_850_000m,
        0.9m,
        0.1m,
        1m,
        100m,
        300m,
        0.45m);
}
