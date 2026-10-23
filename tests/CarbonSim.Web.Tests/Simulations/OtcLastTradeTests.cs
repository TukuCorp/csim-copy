using System.Globalization;
using System.Security.Claims;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Web.Player;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// An OTC deal is a trade: the exchange summary shows it as the product's OTC last trade, and the
/// top bar's last trade is the latest deal in either market (demo 18:19, "here's the last trade that
/// went off" after an OTC deal).
/// </summary>
public sealed class OtcLastTradeTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public OtcLastTradeTests(SimulationTestHost host) => _host = host;

    [Fact]
    public async Task An_otc_deal_is_the_last_trade_until_the_exchange_trades_again()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Guid id = runs.Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SignedIn redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);
        await _host.CreateDriver().StartAsync(id);
        simulation.Ledger.Grant(simulation.FindUnit(1).Company, Product.Allowance(1), 1_000m);
        PlayerSession session = Session(delta.AccountId);

        long offer = await runs.SendOtcOfferAsync(id, delta.Actor, 1, 2, "Vintage 1", 113m, 100m);
        await runs.AnswerOtcOfferAsync(id, redRiver.Actor, 2, offer, true);

        ProductBookView afterOtc = Vintage1(await session.ForPlayerAsync(id));

        afterOtc.OtcLastTrade.Should().Be(113m);
        afterOtc.MarketLastTrade.Should().Be(113m, "the OTC deal is the latest trade in the product");

        await runs.PlaceOrderAsync(id, delta.Actor, 1, "Vintage 1", "Sell", "Limit", 100m, 110m);
        await runs.PlaceOrderAsync(id, redRiver.Actor, 2, "Vintage 1", "Buy", "Limit", 100m, 110m);

        ProductBookView afterExchange = Vintage1(await session.ForPlayerAsync(id));

        afterExchange.OtcLastTrade.Should().Be(113m);
        afterExchange.MarketLastTrade.Should().Be(110m);
    }

    private static ProductBookView Vintage1(PlayerView? view) =>
        view!.Books.Single(book => book.Product.Key == "Vintage 1");

    private PlayerSession Session(int accountId) => new(
        _host.Server.Services.GetRequiredService<SimulationRegistry>(),
        _host.Server.Services.GetRequiredService<IServiceScopeFactory>(),
        new FixedAuthenticationStateProvider(accountId));

    /// <summary>Signs a session in without a Blazor circuit: the account id a real cookie carries.</summary>
    private sealed class FixedAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;

        public FixedAuthenticationStateProvider(int accountId)
        {
            _user = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, accountId.ToString(CultureInfo.InvariantCulture))],
                authenticationType: "test"));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_user));
    }
}
