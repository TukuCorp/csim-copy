using Bunit;
using CarbonSim.Web.Components.Layout;
using CarbonSim.Web.Components.Shared;
using CarbonSim.Web.Player;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Components;

/// <summary>The shell around every screen: the top bar, the progress strip and the left nav.</summary>
public sealed class ShellTests : ScreenTestContext
{
    public ShellTests()
    {
        View = PlayerViewFixture.Sample(SimulationId);
    }

    [Fact]
    public void The_layout_shows_the_top_bar_the_progress_strip_and_the_nav_for_a_run()
    {
        Navigate($"/run/{SimulationId:D}/dashboard");

        IRenderedComponent<MainLayout> layout = RenderComponent<MainLayout>(parameters => parameters
            .Add(component => component.Body, (RenderFragment)(_ => { })));

        layout.Markup.Should().Contain(PlayerViewFixture.CompanyName);
        layout.Markup.Should().Contain("Simulation progress");
        layout.Markup.Should().Contain("Section progress");
        layout.Markup.Should().Contain("Allowance auction");
        layout.FindAll("nav a").Should().HaveCount(11);
    }

    [Fact]
    public void The_layout_leaves_a_page_without_a_run_to_itself()
    {
        Navigate("/");

        IRenderedComponent<MainLayout> layout = RenderComponent<MainLayout>(parameters => parameters
            .Add(component => component.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>plain</p>"))));

        layout.Markup.Should().Contain("plain");
        layout.FindAll("nav").Should().BeEmpty();
    }

    [Fact]
    public void The_top_bar_shows_the_company_and_the_quote()
    {
        IRenderedComponent<TopBar> bar = RenderComponent<TopBar>(parameters => parameters.AddCascadingValue(Snapshot));

        bar.Markup.Should().Contain(PlayerViewFixture.CompanyName);
        bar.Markup.Should().Contain("Best offer");
        bar.Markup.Should().Contain("Last trade");
        bar.Markup.Should().Contain("Best bid");
        bar.Markup.Should().Contain("Net revenue this year");
        bar.Markup.Should().Contain("112.00");
    }

    [Fact]
    public void The_progress_strip_counts_down_and_flags_a_paused_clock()
    {
        IRenderedComponent<ProgressStrip> strip = RenderComponent<ProgressStrip>(parameters => parameters.AddCascadingValue(Snapshot));

        strip.Markup.Should().Contain("Allowance auction ends in 3m 00s");
        strip.Markup.Should().Contain("Auction 2");

        View = Paused(View);
        Session.View = View;

        IRenderedComponent<ProgressStrip> paused = RenderComponent<ProgressStrip>(parameters => parameters.AddCascadingValue(Snapshot));

        paused.Markup.Should().Contain("PAUSED");
    }

    [Fact]
    public void The_rules_panel_spells_out_the_parameters()
    {
        IRenderedComponent<RulesPanel> rules = RenderComponent<RulesPanel>(parameters => parameters.Add(component => component.View, View));

        rules.Markup.Should().Contain("Compliance cost calculator");
        rules.Markup.Should().Contain("Auction floor price");
    }

    [Theory]
    [InlineData("/run/4f8b1c2e-9a3d-4f6b-8c1e-2b5a7d9f0e31/auction", "4f8b1c2e-9a3d-4f6b-8c1e-2b5a7d9f0e31")]
    [InlineData("http://localhost/run/4f8b1c2e-9a3d-4f6b-8c1e-2b5a7d9f0e31/otc", "4f8b1c2e-9a3d-4f6b-8c1e-2b5a7d9f0e31")]
    [InlineData("/", null)]
    [InlineData("/sign-in", null)]
    public void The_run_route_reads_the_simulation_out_of_the_address(string address, string? expected)
    {
        Guid? id = RunRoute.SimulationId(address);

        if (expected is null)
        {
            id.Should().BeNull();
        }
        else
        {
            id.Should().Be(Guid.Parse(expected));
        }
    }

    private void Navigate(string address)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(address);
    }

    private static PlayerView Paused(PlayerView view) => view with { State = "Paused", AuctionOpen = false };
}
