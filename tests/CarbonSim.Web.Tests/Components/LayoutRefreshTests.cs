using Bunit;
using CarbonSim.Web.Components.Layout;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The shell's once-a-second refresh is what keeps a player's countdowns and numbers moving. One
/// failed read - a run deleted under the screen, a database hiccup - must not stop it for good.
/// </summary>
public sealed class LayoutRefreshTests : ScreenTestContext
{
    public LayoutRefreshTests()
    {
        View = PlayerViewFixture.Sample(SimulationId);
    }

    protected override TimeSpan? RefreshInterval => TimeSpan.FromMilliseconds(20);

    [Fact]
    public void The_refresh_keeps_going_after_one_read_fails()
    {
        Navigate($"/run/{SimulationId:D}/dashboard");

        IRenderedComponent<MainLayout> layout = RenderComponent<MainLayout>(parameters => parameters
            .Add(component => component.Body, (RenderFragment)(_ => { })));

        layout.WaitForAssertion(() => Session.Reads.Should().BeGreaterThan(1), TimeSpan.FromSeconds(5));
        Session.FailNextRead = new InvalidOperationException("The run is gone.");
        int failedAt = Session.Reads + 1;

        layout.WaitForAssertion(
            () => Session.Reads.Should().BeGreaterThan(failedAt + 1, "the refresh carried on past the failed read"),
            TimeSpan.FromSeconds(5));
    }

    private void Navigate(string address) =>
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
}
