using System.Globalization;
using Bunit;
using CarbonSim.Web.Player;
using CarbonSim.Web.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// A player session a screen test drives: it hands back one prepared view and records every action
/// a component asked for, so a button can be asserted on what it did rather than on what it drew.
/// </summary>
public sealed class StubPlayerSession : IPlayerSession
{
    public PlayerView? View { get; set; }

    public IReadOnlyList<RunSummary> Runs { get; set; } = [];

    public List<string> Actions { get; } = [];

    public Task<IReadOnlyList<RunSummary>> RunsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Runs);

    public Task<string?> CurrentActorAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>("1");

    public Task<PlayerView?> ForPlayerAsync(Guid simulationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(View);

    public Task PlaceBidAsync(Guid simulationId, int unitId, int vintage, decimal price, decimal volume, CancellationToken cancellationToken = default)
    {
        // Invariant, so an assertion in this file reads the same whatever culture the test runs in.
        Actions.Add(FormattableString.Invariant($"bid:{unitId}:{vintage}:{price}:{volume}"));
        return Task.CompletedTask;
    }

    public Task PlaceOrderAsync(Guid simulationId, int unitId, string product, string side, string kind, decimal volume, decimal? price, decimal? stopPrice, string fillPolicy, CancellationToken cancellationToken = default)
    {
        Actions.Add(FormattableString.Invariant($"order:{unitId}:{product}:{side}:{kind}:{volume}:{price}:{stopPrice}:{fillPolicy}"));
        return Task.CompletedTask;
    }

    public Task CancelOrderAsync(Guid simulationId, int unitId, long orderId, CancellationToken cancellationToken = default)
    {
        Actions.Add(FormattableString.Invariant($"cancel:{unitId}:{orderId}"));
        return Task.CompletedTask;
    }

    public Task ImplementAbatementAsync(Guid simulationId, int unitId, int optionIndex, CancellationToken cancellationToken = default)
    {
        Actions.Add(FormattableString.Invariant($"implement:{unitId}:{optionIndex}"));
        return Task.CompletedTask;
    }

    public Task SendOtcOfferAsync(Guid simulationId, int sellerUnitId, int buyerUnitId, string product, decimal price, decimal volume, CancellationToken cancellationToken = default)
    {
        Actions.Add(FormattableString.Invariant($"otc-offer:{sellerUnitId}:{buyerUnitId}:{product}:{price}:{volume}"));
        return Task.CompletedTask;
    }

    public Task AnswerOtcOfferAsync(Guid simulationId, int buyerUnitId, long offerId, bool accept, CancellationToken cancellationToken = default)
    {
        Actions.Add(FormattableString.Invariant($"otc-answer:{buyerUnitId}:{offerId}:{accept}"));
        return Task.CompletedTask;
    }

    public Task PostMessageAsync(Guid simulationId, string text, CancellationToken cancellationToken = default)
    {
        Actions.Add($"message:{text}");
        return Task.CompletedTask;
    }
}

/// <summary>Remembers what each chart was asked to draw, so a chart test can read the option back.</summary>
public sealed class RecordingCharts : ICharts
{
    public List<string> Options { get; } = [];

    public Task RenderAsync(ElementReference element, string optionJson)
    {
        Options.Add(optionJson);
        return Task.CompletedTask;
    }

    public Task DisposeAsync(ElementReference element) => Task.CompletedTask;
}

/// <summary>
/// The base for a screen test: a stub session holding one prepared view, a recording chart surface,
/// and the real string localiser so a missing or miswritten resource key fails the test.
/// </summary>
public abstract class ScreenTestContext : TestContext
{
    protected ScreenTestContext()
    {
        // QuickGrid imports its own browser module when it renders; a screen test does not need the
        // module's answer, so any JavaScript a component asks for is allowed to return nothing.
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLocalization();
        Services.AddSingleton<IPlayerSession>(Session);
        Services.AddSingleton<ICharts>(Charts);
        Services.AddSingleton(new PlayerViewOptions { RefreshInterval = null });
        Services.AddSingleton(provider =>
        {
            PlayerContext context = new(provider.GetRequiredService<IPlayerSession>());

            // The shell attaches to a run before its pages render; the test does the same, so an
            // action a page sends has a run to go to.
            context.AttachAsync(SimulationId).GetAwaiter().GetResult();

            return context;
        });
    }

    protected StubPlayerSession Session { get; } = new();

    protected RecordingCharts Charts { get; } = new();

    protected Guid SimulationId { get; } = Guid.Parse("4f8b1c2e-9a3d-4f6b-8c1e-2b5a7d9f0e31");

    /// <summary>The view the screens render; keeping the stub session in step is what a shell refresh would do.</summary>
    protected PlayerView View
    {
        get => _view;
        set
        {
            _view = value;
            Session.View = value;
        }
    }

    private PlayerView _view = null!;

    protected PlayerScreenSnapshot Snapshot => new(SimulationId, View, null, null);

    protected static IStringLocalizer<SharedStrings> Localizer(IServiceProvider services) =>
        services.GetRequiredService<IStringLocalizer<SharedStrings>>();
}
