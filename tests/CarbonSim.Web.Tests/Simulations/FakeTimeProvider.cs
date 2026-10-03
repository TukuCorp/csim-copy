namespace CarbonSim.Web.Tests.Simulations;

/// <summary>A hand-driven clock for the web tests: the hosted driver reads its time through
/// this, so a test advances virtual time explicitly and never sleeps.</summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan amount) => _now = _now.Add(amount);
}
