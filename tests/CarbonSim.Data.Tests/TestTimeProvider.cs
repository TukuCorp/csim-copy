namespace CarbonSim.Data.Tests;

/// <summary>
/// Wall time the engine reads when it advances the clock. Tests move it by hand, so a virtual
/// year takes milliseconds and nothing sleeps. Two runs being compared each get their own
/// provider, started from the same instant, so that playing one does not drag the other's
/// clock along with it while still giving both the same elapsed time.
/// </summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public TestTimeProvider(DateTimeOffset? start = null)
    {
        _now = start ?? new DateTimeOffset(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan amount) => _now += amount;
}
