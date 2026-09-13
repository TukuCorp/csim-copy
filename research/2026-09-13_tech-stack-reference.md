# Tech stack reference: what CarbonSim runs on, and what to build the clone on

Date: 2026-09-13
Scope: the three technologies observed in the original (ASP.NET MVC, SignalR, dygraphs), their current status, and modern equivalents. Companion to `2026-09-13_carbonsim-replication-assessment.md`.

## 1. The original stack, as observed on sim3.carbonsim.org

Evidence: HTTP response headers, the public login page, `static/generated/scripts/mvc-vendor.js` (722 KB) and `mvc-bundle.js` (30 KB). Nothing was decompiled; version strings are self-declared in the bundles.

| Layer | Component | Version | Notes |
|---|---|---|---|
| Server | ASP.NET MVC on .NET Framework | MVC 5.2, CLR 4.0.30319 | `X-AspNetMvc-Version: 5.2`, `X-AspNet-Version: 4.0.30319` |
| Host | IIS | 10.0 | Windows Server |
| Real-time | ASP.NET SignalR (classic) | 2.1.2 | jQuery-based `$.connection` hub proxies; auction and exchange live updates, market open/close, trading-halt warnings, OTC alerts, messages, clock |
| DOM / UI | jQuery, jQuery UI, Bootstrap | 1.11.1, 1.11.1, 3.2.0 custom build | Bootstrap built without xs/sm grid: desktop-only |
| Time-series charts | dygraphs | 1.0.1 | Close-price graph on Dashboard and Exchange |
| Candlestick charts | techan.js on d3 | pre-1.0 | Candlestick view of the exchange |
| Tables | DataTables | 1.10.2 | Order books, auction history, leaderboard |
| Utilities | Underscore 1.7, moment.js, toastr, excanvas | | excanvas is an IE8 canvas shim: codebase dates from ~2014 |
| Localisation | server resource strings + `cultureName` cookie | | 883 UI keys served at `/home/resourcestrings`; 9 cultures |
| Data | not visible | | GUID primary keys in HTML suggest SQL Server with Entity Framework |

Conclusion: a 2014-era server-rendered MVC app with a jQuery front end and SignalR push. Functionally complete, technically end-of-era.

## 2. Status of each technology today (September 2026)

### ASP.NET MVC 5 (Framework) versus ASP.NET Core
- ASP.NET MVC 5.x is feature-frozen. It lives only on .NET Framework 4.8, whose support is tied to the Windows OS lifecycle, Windows-only, no new APIs.
- ASP.NET Core is the live line. .NET 10 is the current LTS (released 11 Nov 2025, supported to 14 Nov 2028). .NET 8 and .NET 9 both reach end of support on 10 Nov 2026.
- Local machine: .NET SDK 9.0.312 installed, Node 24.12, Python 3.14. Install the .NET 10 SDK before starting; .NET 9 will be unsupported within two months.
- Verdict: do not build new on MVC 5. ASP.NET Core 10 gives the same MVC/Razor programming model plus minimal APIs, built-in DI, cross-platform hosting (Linux containers), and SignalR in the box.

### SignalR
- Classic SignalR 2.x (what the original uses) is legacy and tied to .NET Framework.
- ASP.NET Core SignalR ships with ASP.NET Core; JavaScript client is the `@microsoft/signalr` npm package; also .NET, Java, Swift clients. WebSockets with automatic fallback; open hub protocol; optional MessagePack.
- Programming model that maps directly onto CarbonSim's needs:
  - one `Hub` subclass per concern (e.g. `SimulationHub`), clients call server methods with `connection.invoke(...)`, server pushes with `Clients.All / Group / User`;
  - **groups** map naturally to "one simulation" or "one trading system" so parallel simulations do not leak events;
  - **`IHubContext<T>`** lets a background service (the game clock, auction closer, year-end reconciler) push events without being inside a hub;
  - **strongly typed hubs** (`Hub<ISimulationClient>`) give compile-time names for events such as `AuctionOpened`, `AuctionPreEndAlert`, `TradingHalted`, `OfferReceived`, all of which exist as strings in the original's catalog.
- Scale-out via Redis backplane exists but is unnecessary at 40 to 250 concurrent players on one server.
- Verdict: keep SignalR. It is the strongest reason to stay on .NET for this project.

### dygraphs
- Still maintained: v2.2.2, MIT, commits as recent as July 2026, ESM build on npm, ~3.2k stars. Line and (via plugin) candlestick charts, fast canvas rendering, good for long time series.
- The original used 1.0.1; the API is broadly compatible but the modern build is ESM/TypeScript friendly.
- Verdict: acceptable for the close-price line, but it is a time-series specialist; it does not cover MACC bars, N.O.P. bars, or the intensity gauge that the abatement screen also shows.

### techan.js (the original's candlestick library)
- Archived. Last commit October 2016, pinned to d3 v4, pre-1.0.
- Verdict: do not use. Replace with one of the options below.

## 3. Charting options for the clone

The original shows five chart types: candlestick (exchange), close-price line (dashboard, exchange, per vintage and offsets), MACC step/variable-width bars (abatement), N.O.P. paired bars, and an intensity gauge.

| Option | Covers | Licence | Size | Fit |
|---|---|---|---|---|
| Apache ECharts | candlestick, line, bar, custom series (variable-width MACC), gauge | Apache-2.0 | ~1 MB (tree-shakeable) | One library for every chart on every screen; canvas; good with 5 to 10 series per vintage |
| TradingView Lightweight Charts | candlestick, line, histogram, area | Apache-2.0 | 35 KB | Best-looking trading charts; needs a second library for MACC and gauge; x-axis is real-date based, so virtual years must be mapped to synthetic dates (the original already labels axes "Jan 13, Year 1", so this is natural) |
| dygraphs 2.x | line, candlestick plugin | MIT | ~120 KB | Faithful to the original's feel; needs a second library for bars and gauge |
| Chart.js + financial plugin | line, bar, candlestick | MIT | ~70 KB | Simple; candlestick plugin is community-maintained |

Recommendation: ECharts as the single charting dependency. If the trading screens must feel like a real exchange terminal, use Lightweight Charts for the exchange page only and ECharts elsewhere.

## 4. Recommended stack for the clone

| Layer | Choice | Why |
|---|---|---|
| Runtime | .NET 10 LTS | supported to Nov 2028; SignalR built in |
| Engine | plain C# class library (`CarbonSim.Engine`), no web dependencies | rules, auction clearing, order matching, reconciliation, bots; unit-tested with xUnit first (TDD) |
| Persistence | EF Core 10; SQLite for dev and single-box training deployments, PostgreSQL if hosted | GUID keys like the original; simulations are small (thousands of rows) |
| Web host | ASP.NET Core 10 | serves UI, hubs, admin API |
| Real-time | ASP.NET Core SignalR, groups per simulation, `IHubContext` from a hosted `SimulationClockService` | direct mapping of the original's live events |
| Player and admin UI | Path A: Blazor Server (interactive server render mode). Path B: React + Vite + `@microsoft/signalr` | A keeps one language and needs no JS build; B gives a richer trading UI and easier chart integration |
| Charts | Apache ECharts (JS interop in Blazor, or React wrapper) | single library covers all five chart types |
| Tables | QuickGrid (Blazor) or TanStack Table (React) | replaces DataTables |
| Localisation | `.resx` resources keyed like the original's 883 strings; culture cookie | lets the Vietnamese glossary be dropped in as a translation file |
| Styling | Bootstrap 5 or Tailwind | original used Bootstrap 3 desktop-only; make it responsive this time |

Path A versus B: Blazor Server is the faster route for a small team that is fluent in C# and wants the server to own all game state (it already must). React is better if the exchange screen needs high-frequency order-book rendering or if a separate front-end developer is involved. Either way the engine, hubs and persistence are identical, so the choice can be made after the engine exists.

## 5. Mapping the original's real-time events onto hub methods

From the string catalog (`research/carbonsim-sources/sim3-instance/resourcestrings_en.json`):

| Original message key | Hub event (server to client) | Trigger |
|---|---|---|
| `AuctionPreStartAlert`, `AuctionStartAlert`, `AuctionOpened_X_Vintage_X` | `AuctionOpened(auctionNo, vintage)` | clock service |
| `AuctionPreEndAlert`, `AuctionClosed_X_Vintage_X` | `AuctionClosing(secondsLeft)`, `AuctionCleared(result)` | clock service, clearing |
| `MarketOpened`, `MarketClosed`, `SecondaryMarketTradingHalted`, `WarnBeforeShutdownSeconds` | `MarketStateChanged(state)` | admin or clock |
| `ExchangeMarketLiveUpdates`, `LiveAllowanceAuctionUpdates` | `OrderBookChanged(vintage)`, `TradeExecuted(trade)` | matching engine |
| `TradeOfferAlert`, `OfferAccepted`, `OfferRejected`, `OfferCancelled` | `OtcOfferReceived`, `OtcOfferResolved` | OTC service |
| `MessageAlert` | `MessageReceived(from, text)` | messaging |
| `GameState_*`, `Simulation_Step_X_Year_Y_S_Stage_X_State_X` | `SimulationStateChanged(step, year, stage, state)` | admin |
| `CurrentSimulationHasBeenUpdated` | `ParametersChanged()` | admin shock |

## 6. Legal and hygiene notes
- EDF's terms prohibit reverse engineering and derivative works. The vendor bundle was inspected only for version strings; do not copy any of its code or assets into the clone. The string catalog is used as a feature checklist, not as content to ship verbatim.
- Do not link users to vncarbonmarket.com; the domain is hijacked.

## 7. Immediate actions before coding
1. Install the .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`).
2. Decide Path A (Blazor Server) or Path B (React); the engine work can start regardless.
3. Create `CLAUDE.md` recording the stack, xUnit as the test framework, and the TDD rule, then `activeContext.md` with the phased plan.
