using FluentAssertions;
using Xunit.Abstractions;

namespace CarbonSim.Web.Tests.Load;

/// <summary>
/// Marks the full load run. It takes about twenty minutes of real time - the point is the
/// uncompressed clock - so it is opt-in rather than part of the gate; the smoke test guards the
/// harness itself in the normal pass.
/// </summary>
internal sealed class LoadFullFactAttribute : FactAttribute
{
    public LoadFullFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CARBONSIM_LOAD_FULL") != "1")
        {
            Skip = "The full load run is opt-in. Run: $env:CARBONSIM_LOAD_FULL='1'; "
                + "dotnet test tests/CarbonSim.Web.Tests --filter FullyQualifiedName~LoadFullTests";
        }
    }
}

/// <summary>
/// The Phase 6 load acceptance: 250 connected clients, each refreshing as the production screens
/// do, through a whole virtual year at the shipped twenty-minute length, with a quarter of the
/// room bidding and ordering. No auction may be missed and no refresh may fail.
/// </summary>
public sealed class LoadFullTests
{
    private readonly ITestOutputHelper _output;

    public LoadFullTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [LoadFullFact]
    public async Task Two_hundred_and_fifty_clients_run_a_full_twenty_minute_year()
    {
        LoadMetrics metrics = await LoadHarness.RunAsync(LoadOptions.Full, _output.WriteLine);

        _output.WriteLine(metrics.Describe());

        metrics.Clients.Should().Be(250);
        metrics.Refreshes.Should().BeGreaterThan(250, "the room must refresh for the whole year");
        metrics.RefreshErrors.Should().Be(0, "no refresh may fail under load");
        metrics.Auctions.Should().HaveCount(LoadScenario.AuctionsPerYear);
        metrics.MissedAuctions.Should().Be(0, "every auction must clear at its scheduled close");
    }
}
