namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The browser walks run one class at a time. Each class boots a host of its own and launches
/// Chromium, and running five of those at once on one machine makes the walks time out for
/// reasons that have nothing to do with the screens they are checking.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PlaywrightCollection
{
    public const string Name = "playwright";
}
