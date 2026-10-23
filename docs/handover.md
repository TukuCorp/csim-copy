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

Inside the engine the shape is: a Simulation holds the state and the scenario; the run module
(Engine/Run) owns the order of operations and returns a typed result for each verb; the host
(Web/Simulations) holds the one gate every mutation goes through and broadcasts from that result. Every
screen reads a copy built while the gate is held, never a live engine object.

    Engine             Simulation state: AllocationPlan, Abatement, AllowanceLedger, Cash, Compliance
    Engine/Run         SimulationRun owns the verbs; each returns a RunResult (notices, fills, books, order)
    Engine/Allocation  Standing: the one answer to "where does this company stand this year"
    Engine/Market      Auction, Exchange (OrderBook.FitToBand), Otc, TradingWindow (who may trade when)
    Engine/Reporting   Scoring, Leaderboard, SystemReport -> SystemTotals
    Web/Simulations    SimulationRegistry: the gate and ApplyAsync (verb -> collect -> invalidate -> broadcast)
    Web/Simulations    SimulationClockService: the tick loop and the trainer's controls; HostedRun holds a live run
    Web/Player         PlayerSession: the PlayerView read model; actions return typed results (OrderOutcome)
    Web/Admin          AdminSession: the AdminRunDetail read model and the console's actions
    Web/Components     Blazor screens; Display formats every figure they share

The engine owns state; the host owns time. A run advances only when something calls the driver,
which in production is a hosted service ticking once a second.

## 3. Where each mechanic lives

| Mechanic | File |
|---|---|
| Virtual-year clock, states, auction sections | src/CarbonSim.Engine/Clock/SimulationClock.cs |
| The run module: verbs, order of operations, typed results | src/CarbonSim.Engine/Run/SimulationRun.cs, RunFacts.cs |
| The current rules (the Parameters accessor) | src/CarbonSim.Engine/Domain/Simulation.cs (Simulation.Rules) |
| Cap, sector cakes, free allocation, BAU growth | src/CarbonSim.Engine/Allocation/AllocationPlan.cs |
| Where a company stands: emissions, held, available, shortfall, position | src/CarbonSim.Engine/Allocation/Standing.cs |
| Abatement lifecycle (build, operate, expire, shutdown) | src/CarbonSim.Engine/Abatement/ |
| Sealed-bid uniform-price auction, collar, forward vintages | src/CarbonSim.Engine/Market/Auction.cs, AuctionSchedule.cs |
| Government reserve and auction revenue | src/CarbonSim.Engine/Market/GovernmentAccount.cs |
| Exchange order book (market, limit, stop, partial fill, band, FitToBand) | src/CarbonSim.Engine/Market/Exchange/ |
| The outcome of an exchange order (fills, resting, refusal) | src/CarbonSim.Engine/Run/OrderOutcome.cs |
| OTC offers | src/CarbonSim.Engine/Market/Otc/ |
| Instrument balances and escrow | src/CarbonSim.Engine/Market/AllowanceLedger.cs |
| Year-end surrender, banking, penalties, fines | src/CarbonSim.Engine/Compliance/ComplianceRegister.cs |
| Capital, overdraft and interest, operating profit | src/CarbonSim.Engine/Finance/ |
| Marginal cost of compliance, leaderboard, system report | src/CarbonSim.Engine/Reporting/ |
| Rule-based bots and the AutoTrade fleet | src/CarbonSim.Engine/Bots/ |
| Scenario JSON loader and validation | src/CarbonSim.Engine/Scenarios/ScenarioLoader.cs |
| The save/restore boundary (every drawn value is carried) | src/CarbonSim.Engine/Snapshot/ |
| EF model and the engine-to-table map | src/CarbonSim.Data/CarbonSimDbContext.cs, Persistence/SimulationMapper.cs |
| Live runs, the per-run gate, the one ApplyAsync primitive, the event feed | src/CarbonSim.Web/Simulations/SimulationRegistry.cs |
| Run-view helpers both sessions share (year-of, reserve) | src/CarbonSim.Web/Simulations/RunViews.cs |
| The tick loop and admin controls | src/CarbonSim.Web/Simulations/SimulationClockService.cs |
| Player read model and its shared view cache | src/CarbonSim.Web/Player/PlayerSession.cs |
| Admin read model and controls | src/CarbonSim.Web/Admin/ |
| Player and admin screens | src/CarbonSim.Web/Components/ |
| Styles (palette, focus ring, phone layout) | src/CarbonSim.Web/wwwroot/app.css |

## 4. Build, test, run, deploy

    dotnet build CarbonSim.sln          # zero warnings, warnings are errors
    dotnet test CarbonSim.sln           # everything, Playwright included
    dotnet format CarbonSim.sln         # house style
    dotnet run --project src/CarbonSim.Web          # development, localhost only
    powershell -File scripts/run-demo.ps1           # the internal demo host

The solution targets net10.0 (LTS, supported to November 2028); Directory.Build.props holds the
single TargetFramework line. .github/workflows/ci.yml runs the same gate (build, format, test with Playwright) on windows-latest.

The full load run is opt-in (about twenty minutes of real time):

    $env:CARBONSIM_LOAD_FULL = '1'
    dotnet test tests/CarbonSim.Web.Tests --filter FullyQualifiedName~LoadFullTests

Deployment is a published build on one host started by the script; there are no containers. The
detail (configuration, tunnels, TLS, PostgreSQL, backup and restore, password resets) is in
[deployment.md](deployment.md). Running the two-day agenda is in
[trainer-runbook.md](trainer-runbook.md), and the guide for internal testers is
[tester-guide.md](tester-guide.md).

## 5. Known gaps

**One fidelity envelope stays open.** Since 2026-10-23 the envelopes hold the clone to what the
original was observed doing (the owner's decision; sources in reports/fidelity/audit-c-numbers.md,
section 3). The seeded three-year run of scenarios/vietnam-2024.json holds all of them but the
leaderboard spread: every company-year complies (726 of 726, no penalties), year 1 clears at 126.63
(the original's demo 123.55) and prices rise to 250.38 by year 3. The spread runs from -68.64 to
108.80 per tonne against the lecture's +/-40; it comes from the scenario's price level and its
allocation spread rather than from the bots, so it is a skipped measurement until the scenario is
re-authored. Nothing is hand-tuned to pass. The numbers live in
tests/CarbonSim.Engine.Tests/Golden/fidelity-golden.md, which the suite asserts on every run.

**A crash loses the year in progress.** Runs are saved when created and at each year end. A reload
replays that year; the pending notice queues are not saved, so one auction notice may repeat.

**Single display currency per deployment.** Display.Currency is ambient host state; two languages
wanting different currencies on one box is not supported (one currency, two languages is).

**PostgreSQL is switchable but unexercised.** The provider is chosen in configuration and the model
scripts as PostgreSQL SQL, but the schema has not been applied to a live server. The shipped
migration set is SQLite-only; PostgreSQL builds from the model.

**One trading system per simulation**, as the snapshot and the registry both assume.

**Admin setup covers the exercise, not the roster.** The setup screen edits the rules, the AI
difficulty, each company's name, capital and credit limit, the sectors (with an optional free
allocation share each) and their menus. Adding or deleting companies and units, a per-vintage
schedule of sales and setup import/export still come from the scenario JSON (audit B #33, #35, #36,
#40).

**Manual surrender covers the surrender, not the bank.** In a manual exercise a human company sets
how many allowances and offsets it surrenders (`ComplianceRegister.SetSurrender`, one plan behind the
year end and the screen's penalty preview), but what is left is still banked automatically up to the
limit; the original's separate bank volume (`VolumeToBeBanked_tCO2e`) is not built.

**The trade history is per trade, not per order.** The dashboard lists the company's own trades with
side and cost; the original's order-centric history (order type, partial fill, price limit, target
volume) needs the exchange to keep closed orders, which it does not.

**A mid-run change moves the collar, the compliance rules and the market switches only.** The
trainer can move the collar, change the offset usage limit, banking limit and cash penalty
(`SimulationRun.ChangeComplianceRules`), and open or close the exchange, the OTC market and the
leaderboard inside a year (`SimulationRun.SetMarketAccess`); the cap, allocation and auction lots
are fixed when an exercise is built.

## 6. Prioritised next steps

1. Run the human dry run in docs/tester-guide.md section 9 on the demo box, then the internal
   session; file findings with the Demo feedback issue template.
2. Push the branch so .github/workflows/ci.yml runs once and is proven on a hosted runner.
3. Split a bot's plan from its execution (architecture candidate 6), with the golden as the net.
4. Verify the PostgreSQL path against a real server before any multi-box deployment.
5. Settle the licensing question with EDF before any use outside the company.
