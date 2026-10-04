using FluentAssertions;

namespace CarbonSim.Web.Tests.Load;

/// <summary>
/// The fast guard on the load harness: a dozen players refresh through a compressed virtual year and
/// every auction still has to clear at its close. It runs in the normal test pass, so the harness
/// and the shared view cache behind it cannot rot without the suite noticing.
/// </summary>
public sealed class LoadSmokeTests
{
    [Fact]
    public async Task A_dozen_clients_through_a_compressed_year_clear_every_auction()
    {
        LoadMetrics metrics = await LoadHarness.RunAsync(LoadOptions.Smoke);

        metrics.Clients.Should().Be(12);
        metrics.Refreshes.Should().BeGreaterThanOrEqualTo(metrics.Clients, "every session must actually have refreshed");
        metrics.RefreshErrors.Should().Be(0, "no refresh may fail under load");
        metrics.Auctions.Should().HaveCount(LoadScenario.AuctionsPerYear, "one virtual year holds four auctions");
        metrics.MissedAuctions.Should().Be(0, "no auction may be missed");
        metrics.GateWaitMax.Should().BeLessThan(TimeSpan.FromSeconds(5), "the run gate must not stall a screen");
    }
}
