using Bunit;
using CarbonSim.Web.Components.Pages;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The room waits on the front door while the trainer sets the exercise up. The screen has to see
/// the exercise appear by itself: nobody in a training room knows to press reload.
/// </summary>
public sealed class WaitingRefreshTests : ScreenTestContext
{
    protected override TimeSpan? RefreshInterval => TimeSpan.FromMilliseconds(20);

    [Fact]
    public void The_front_door_shows_an_exercise_created_while_it_was_open()
    {
        IRenderedComponent<Waiting> page = RenderComponent<Waiting>();
        page.Markup.Should().NotContain("Vietnam ETS Pilot 2024");

        Session.Runs = [WaitingScreenTests.Summary()];

        page.WaitForAssertion(() => page.Markup.Should().Contain("Vietnam ETS Pilot 2024"), TimeSpan.FromSeconds(5));
    }
}
