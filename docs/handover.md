# CarbonSim clone - handover

This is the map for whoever picks the project up next: what it is, where everything lives, how to
build, test, run and deploy it, what is deliberately not finished, and what to do first.

## 1. What this is

A from-scratch, behaviourally faithful clone of EDF CarbonSim: a multi-user emissions-trading
system training game. A trainer configures an exercise (sectors, companies, units, abatement menus,
ETS parameters), players run virtual companies that must comply with a declining cap at least cost,
and rule-based bots trade alongside them. Compliance is possible through free allocation, government
auctions, an exchange, OTC trades and abatement, with surrender, banking and penalties at year end.

The original is proprietary and may not be copied. Everything here is written for this project
from public documentation and observed behaviour; research/ is a specification reference, never a
source of strings, styles or assets (see lessons.md).

Fidelity means the acceptance envelopes in
research/2026-09-13_bot-logic-and-numeric-fidelity-options.md section 6, not EDF numbers.

## 2. Architecture map

Three source projects and three test projects.

| Project | What it holds |
|---|---|
| src/CarbonSim.Engine | The whole game, as a pure class library. No web, no database, no clock of its own. |
| src/CarbonSim.Data | EF Core model, one SQLite migration set, the repository, and the account store. |
| src/CarbonSim.Web | ASP.NET Core host: Blazor Server player screens, the admin console, JSON endpoints, the SignalR hub, and the in-process driver. |
| tests/CarbonSim.Engine.Tests | Engine rules and the seeded fidelity run. |
| tests/CarbonSim.Data.Tests | Save/reload round trips, provider selection, the SQLite backup drill. |
| tests/CarbonSim.Web.Tests | bUnit component tests, host integration tests, the load harness, and the Playwright walks (screens, phone width, Vietnamese, admin, accessibility). |

The engine owns state; the host owns time. A run advances only when something calls the driver,
which in production is a hosted service ticking once a second.

## 3. Where each mechanic lives

| Mechanic | File |
|---|---|
| Virtual-year clock, states, auction sections | src/CarbonSim.Engine/Clock/SimulationClock.cs |
| Cap, sector cakes, free allocation, BAU growth | src/CarbonSim.Engine/Allocation/AllocationPlan.cs |
| Long/short forecast position | src/CarbonSim.Engine/Allocation/CompliancePosition.cs |
| Abatement lifecycle (build, operate, expire, shutdown) | src/CarbonSim.Engine/Abatement/ |
| Sealed-bid uniform-price auction, collar, forward vintages | src/CarbonSim.Engine/Market/Auction.cs, AuctionSchedule.cs |
| Government reserve and auction revenue | src/CarbonSim.Engine/Market/GovernmentAccount.cs |
| Exchange order book (market, limit, stop, partial fill, band) | src/CarbonSim.Engine/Market/Exchange/ |
| OTC offers | src/CarbonSim.Engine/Market/Otc/ |
| Instrument balances and escrow | src/CarbonSim.Engine/Market/AllowanceLedger.cs |
| Year-end surrender, banking, penalties, fines | src/CarbonSim.Engine/Compliance/ComplianceRegister.cs |
| Capital, overdraft and interest, operating profit | src/CarbonSim.Engine/Finance/ |
| Marginal cost of compliance, leaderboard, system report | src/CarbonSim.Engine/Reporting/ |
| Rule-based bots and the AutoTrade fleet | src/CarbonSim.Engine/Bots/ |
| Scenario JSON loader and validation | src/CarbonSim.Engine/Scenarios/ScenarioLoader.cs |
| The save/restore boundary (every drawn value is carried) | src/CarbonSim.Engine/Snapshot/ |
| EF model and the engine-to-table map | src/CarbonSim.Data/CarbonSimDbContext.cs, Persistence/SimulationMapper.cs |
| Live runs, per-run gate, event feed, player actions | src/CarbonSim.Web/Simulations/SimulationRegistry.cs |
| The tick loop and admin controls | src/CarbonSim.Web/Simulations/SimulationClockService.cs |
| Player read model and its shared view cache | src/CarbonSim.Web/Player/PlayerSession.cs |
| Admin read model and controls | src/CarbonSim.Web/Admin/ |
| Player and admin screens | src/CarbonSim.Web/Components/ |
| Styles (palette, focus ring, phone layout) | src/CarbonSim.Web/wwwroot/app.css |

## 4. Build, test, run, deploy

    dotnet build CarbonSim.sln          # zero warnings, warnings are errors
    dotnet test CarbonSim.sln           # everything, Playwright included
    dotnet format CarbonSim.sln         # house style
    dotnet run --project src/CarbonSim.Web

The solution targets net9.0. Directory.Build.props holds the single TargetFramework line, which is
the one place to change when the .NET 10 SDK is installed.

The full load run is opt-in (about twenty minutes of real time):

    $env:CARBONSIM_LOAD_FULL = '1'
    dotnet test tests/CarbonSim.Web.Tests --filter FullyQualifiedName~LoadFullTests

Deployment - single server, configuration, TLS and reverse proxy, PostgreSQL, and backup and
restore - is in [deployment.md](deployment.md). Running the two-day training agenda is in
[trainer-runbook.md](trainer-runbook.md).

## 5. Known gaps

**Fidelity envelopes still open.** The seeded three-year run of scenarios/vietnam-2024.json meets
seven of the fidelity envelopes and skips four, each with its measured value in the skip reason:
full compliance at Normal difficulty (346 company-years short), prices climbing to the ceiling by
year three (3.4% of the way), the 2-3% offset surrender share (6.09%, set by the harness
disbursement), and a mid-run price shock inside one virtual month (no shock in the harness).
Closing the first three is the bot-calibration item, not a scenario edit; the fourth needs a shock
in the fidelity harness itself. Nothing is hand-tuned to pass.

**Bot calibration** - the single largest piece of unfinished fidelity work. The bots trade and
comply but do not yet push the price path to the ceiling or reach full compliance at Normal.

**In-memory notice queues.** A run saved mid-auction does not carry its pending notices, so a host
that restarts replays that auction opening or closing notice once. The auction itself is intact.

**Single display currency per deployment.** Display.Currency is ambient host state; two languages
wanting different currencies on one box is not supported (one currency, two languages is).

**tradingOpenShareOfYear is inert.** It is validated, persisted and displayed, but the clock
derives the auction window from AuctionDuration and AuctionsPerYear alone. The shipped scenario
agrees by coincidence; the two can silently drift.

**Per-refresh account lookup.** PlayerSession resolves the signed-in account row from the database
on every screen refresh, and EF logs each query. It is cheap on SQLite but is a redundant round trip
per client per second at scale; caching the account on the scoped session would remove it.

**PostgreSQL is switchable but unexercised.** The provider is chosen in configuration and the model
scripts as PostgreSQL SQL, but no live PostgreSQL server was available, so the schema has not been
applied to one. The shipped migration set is SQLite-only; PostgreSQL builds from the model.

**Docker files are unverified.** Docker is not installed on the machine they were written on, so the
image was not built or run.

**One trading system per simulation**, as the snapshot and the registry both assume.

**AutoTrade rebuilds the bot fleet**, dropping a bot progress and re-drawing its trigger times -
acceptable for a trainer nudge, not a per-unit refinement.

## 6. Prioritised next steps

1. Calibrate the bots to close the four fidelity envelopes (the bot-fidelity brief section 6), with
   the seeded run as the acceptance test.
2. Verify the PostgreSQL path against a real server, and decide whether to ship a PostgreSQL
   migration set instead of the model-built schema.
3. Build the Docker image where Docker exists and run the compose profiles end to end.
4. Make tradingOpenShareOfYear drive the auction window, or remove it from Parameters.
5. Cache the per-session account lookup in PlayerSession.
6. Persist the pending notice queues so a mid-auction restart does not replay a notice.
7. Decide Blazor Server versus a React front end before the player UI grows further (the stack
   decision has been deferred since Phase 0).
8. Settle the licensing question with EDF before any commercial offering in Vietnam.

