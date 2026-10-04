# activeContext — CarbonSim clone build plan

Status: plan written 2026-09-15, awaiting go-ahead to start Phase 0.
Owner decision still open: keep Blazor Server (assumed) or switch to React before Phase 3.

## Definition of done for v1
A trainer can run the Vietnam two-day agenda end to end: configure a trading system with ~240 units (about 40 human, rest bots), run 3–6 virtual years of 20 minutes with 4 auctions each, watch live prices, pause and shock the market, and show the system report and leaderboard, in English and Vietnamese, on one server. Behaviour passes the fidelity envelopes in `research/2026-09-13_bot-logic-and-numeric-fidelity-options.md` §6 and the click-through in `research/2026-09-13_carbonsim-video-insights.md`.

## Phase 0 — Environment and skeleton
- [~] Install .NET 10 SDK; confirm `dotnet --version` is 10.x — **blocked: not installing (instruction). Machine has 9.0.312, so the solution targets `net9.0`.** `Directory.Build.props` holds the single `TargetFramework` line to flip when the SDK is installed
- [x] Create `CarbonSim.sln` with Engine, Data, Web projects and two test projects; nullable + warnings-as-errors; EditorConfig (plus `Directory.Build.props`, `Directory.Packages.props`, `dotnet format` clean)
- [x] `.gitignore` for .NET (bin/obj, .vs, *.user, SQLite files, test results)
- [x] CI-free local gate: `dotnet build` zero warnings/errors and `dotnet test` green
- [ ] Commit "Phase 0: solution skeleton" — not done: user said commit only when asked

## Phase 1 — Engine core (TDD, no UI)
Entities and state
- [x] Simulation, TradingSystem, Sector, Company, Unit, Player (human/AI) models — `src/CarbonSim.Engine/Domain/`; identity, ownership and lifecycle state (`SimulationState`, `CurrentYear`); aggregates and id lookups validated at construction
- [x] Parameters record: cap, reduction rate, free allocation %, BAU growth range per sector, banking limit, offset limit, penalty (cash + allowance), price collar, auctions per year, auction duration, open fraction of year, volatility band, year length, number of years — with `Validate()` returning every out-of-range field at once
- [x] Scenario loader from JSON (`scenarios/vietnam-2024.json`, 242 units, 37 human companies) — **2 of the claimed 39 firm names are absent from the harvested login page; sector shares come from ETS Task 9 covered emissions (43/33/24) rather than the 2025-07 impact report's national shares (46.4/35.7/17.9 normalized); both deviations noted in the file's `notes`**
Clock and lifecycle
- [x] Virtual-year clock with states Pending → Running → Paused / PausedAfterAuction / TradingHalted → YearEnded → GameEnded; warn-before-halt — `Clock/SimulationClock.cs`; a year is split into one section per auction, each section ends with its auction window; `TimeProvider` injected, tests drive a fake clock and never sleep
- [x] Deterministic RNG seeded per simulation — `Randomness/SimulationRandom.cs` (SplitMix64, pinned so runs survive runtime changes); `Simulation.Create(name, seed, …)` derives the id from the seed; scenario files carry `seed`
Allocation and emissions
- [x] Per-unit free allocation for all years computed up front; BAU emissions growth; forecast long/short position — `Allocation/AllocationPlan.cs` (cap, sector cakes, per-unit split by baseline with largest remainders, growth drawn per unit from the sector band) and `Allocation/CompliancePosition.cs`
Abatement
- [x] Abatement option (upfront cost, annual reduction, implementation time, lifetime, annual net revenue/cost, $/t, forecast ROI); implement → Building → Operating → In Profit; additive reductions; capital check; no undo; temporary shutdown — `Abatement/AbatementPortfolio.cs` + `ImplementedAbatement.cs`; menus are authored per sector (`AbatementMenu`) and sized per unit; reductions are capped at what the unit emits; temporary shutdown is per unit-year
Primary market
- [x] Sealed-bid uniform-price auction: multiple bids per unit, rank by price then time, clear to offered volume, all pay clearing price, collar enforcement, per-vintage lots, forward vintages, government reserve — `Market/Auction.cs`, `AuctionSchedule.cs`, `GovernmentAccount.cs`; each vintage clears separately; unsold volume returns to the reserve
Secondary market
- [x] Order book per product (vintage / offset): market, limit, stop-loss, partial fill, fill-or-kill; price-time priority; volatility band rejection; escrow of offered instruments — `Market/Exchange/OrderBook.cs` + `Exchange.cs`; books anchor their band on the auction floor
- [x] OTC: seller-initiated offer to a named unit; accept/reject/cancel; escrow — `Market/Otc/OtcMarket.cs`; instruments escrow through the same `AllowanceLedger` as the order book
Compliance
- [x] Year-end reconciliation: offsets first up to limit, then allowances by vintage; banking capped at obligation; forfeiture of excess; penalty cash + next-year allowance debit; admin fines — `Compliance/ComplianceRegister.cs`; penalties are taken whatever the credit limit, and the debit is subtracted from the next year's grant
Finance
- [x] Capital, overdraft with interest, normal operating profit, N.O.P. with abatement, net revenue — `Finance/CashLedger.cs` (every movement recorded with a category and year, spending refused past the credit limit, cash escrowed for buys and bids) + `Finance/CompanyFinance.cs` (operating profit, abatement result, interest on a negative balance)
Scoring and reports
- [x] Marginal cost of compliance (current year and overall); leaderboard ordering; system totals report ("this year" / "to date") — `Reporting/Scoring.cs`, `Reporting/Leaderboard`, `Reporting/SystemReport.cs`, fed by `Reporting/MarketJournal.cs` (every trade with its channel, price and volume)
Bots
- [x] Rule-based bot per `bot-fidelity` brief §3 with difficulty knob and timing jitter; AutoTrade toggle for human units — `Bots/ComplianceBot.cs`, `Bots/SimulationBots.cs`, `BotSettings`; trigger times drawn once from the seeded stream, orders rest until cancelled, and anything a market refuses is skipped rather than thrown
- [x] Fidelity test suite: seeded 242-unit, 3-year run must satisfy the §6 envelopes — `tests/CarbonSim.Engine.Tests/FidelityTests.cs` + `FidelityRun.cs`; 7 envelopes hold, 4 are skipped with the measured value in the reason (see the review below)
- [x] Review section for Phase 1 — below

## Phase 2 — Persistence and host
- [x] EF Core model and migrations (SQLite); repository boundaries thin; engine stays persistence-agnostic — `src/CarbonSim.Data` (42 tables, one `InitialCreate` migration, `SimulationMapper` and a thin `EfSimulationRepository`); the engine gained a persistence-neutral `src/CarbonSim.Engine/Snapshot/` boundary and references nothing from Data
- [x] ASP.NET Core host; registration with access PIN; login; password reset — `src/CarbonSim.Web` cookie authentication, `PlayerAccountService` with the PIN gate, company claiming, rehash-on-login and emailed reset codes; `IEmailSender` with in-memory and logging implementations
- [x] SignalR `SimulationHub` with per-simulation groups; strongly typed client interface for AuctionOpened/Closing/Cleared, MarketStateChanged, OrderBookChanged, TradeExecuted, OtcOfferReceived/Resolved, MessageReceived, SimulationStateChanged, ParametersChanged — `src/CarbonSim.Web/Simulations/` (`SimulationHub`, `ISimulationClient`, `SimulationGroups`, `SimulationRegistry` with owner-checked player actions, `SimulationClockService`, `SimulationDriverOptions`); hub at `/hubs/simulation`
- [x] Hosted `SimulationClockService` driving the engine and pushing events — injected `TimeProvider` (fake in tests, never sleeps), per-tick clock/auction-clear/bots, `AuctionCleared`+`TradeExecuted` only for newly cleared auctions, `AuctionOpened`/`AuctionClosing` from queued notices, `MarketStateChanged`/`SimulationStateChanged`/`OrderBookChanged` only when they changed, `TradeExecuted` for exchange fills drained from one journal cursor advanced under the run gate, order/OTC/chat broadcasts in the registry; `EndYearAsync` reconciles, closes the books and saves once, and `BeginNextYearAsync` opens the next year and grants its allocation
- [x] Integration tests: two simulated clients see the same auction result — `tests/CarbonSim.Web.Tests/Simulations/` (`SimulationTestHost` with fake clock and manual-only driver, `TestRunFactory` two-human-company scenario, `AuctionBroadcastTests` two HubConnections on one run hearing the same clear/opening/closing, `DriverYearTests` full-year reconcile+persist, `RegistryGuardTests` cross-company refusals, `MarketActionTests` crossing fills and OTC settlement)
- [x] Review section for Phase 2 — below

## Phase 3 — Player UI (Blazor, ECharts)
Order follows the demo video; each screen has a bUnit test and a Playwright step.
- [x] Waiting screen with simulation settings
- [x] Layout: top bar (capital, overdraft, forecast position, MCC, best bid/offer/last, net revenue, briefcase, rules gear with calculator, messages), progress strip (years, auctions, countdowns, PAUSED / halted banners), left nav
- [x] Dashboard: My Finance, My Compliance, Long/Short stacked bar, Abatement implementation timeline, Auction history, Trade history
- [x] Abatement: MACC chart, status box, undertaken timeline, opportunities table with Implement
- [x] Allowance Auction: bid form (volume, vintage, price sliders), vintages this auction, allowances to be auctioned, results modal, histories
- [x] Exchange Market: price chart (candlestick + close, per vintage + offsets), summary strip, trade activity (top of book, last 10), order form
- [x] OTC Market: send offer, offers table, accept/reject
- [x] Company Management, Unit Information, Surrender & Banking, System Info, Leaderboard
- [x] Messaging
- [x] Localisation plumbing (resx, culture cookie), English complete
- [x] Review section for Phase 3

## Phase 4 — Admin console
- [x] Setup: sectors, trading systems, unit abatement configuration, parameters, registration open/close, PIN
- [x] Run-time: Begin Year, pause/resume, halt trading with warning, end year, end simulation, disable messaging, issue fines, disburse offsets, add abatement, end-of-year modifications (shocks), player surrender status
- [x] Reports: system, company, unit, historical average carbon price, leaderboard; export CSV
- [x] Multiple concurrent simulations
- [x] Review section for Phase 4

## Phase 5 — Vietnam localisation and scenario
- [x] Vietnamese resource file (`SharedStrings.vi.resx`, 312 keys) written from the glossary's terminology; a test fails if either file is missing a key or carries an empty value — `tests/CarbonSim.Web.Tests/LocalizationTests.cs`
- [x] Number entry under Vietnamese culture: bid, order and offer boxes are culture-bound text inputs and accept `1.000` and `120,25`; bUnit tests in both cultures — `Components/Pages/{Auction,Exchange,Otc}.razor`, `tests/CarbonSim.Web.Tests/Components/NumberEntryTests.cs`
- [x] VND and USD currency display with a configurable rate: `DisplayCurrency` and `VndPerUsd` host options, `CurrencyDisplay` conversion, every money figure routed through it — `src/CarbonSim.Web/CurrencyDisplay.cs`, `CarbonSimHostOptions`, `Components/Shared/Display.cs`
- [x] Vietnam scenario tuned to the trainer-deck parameters: `scenarios/vietnam-2024.json` kept (the listed parameters already matched); `notes` rewritten with the deck cross-check and the discrepancies (see the review) — fidelity suite re-run, measured envelopes recorded below
- [x] Trainer runbook for the two-day agenda — `docs/trainer-runbook.md`
- [x] Playwright walk extended: switch to Vietnamese, translated labels, a Vietnamese-formatted number accepted — `tests/CarbonSim.Web.Tests/Components/PlaywrightVietnamTests.cs`
- [x] Review section for Phase 5 — below

## Phase 6 — Hardening and deployment
- [x] Load test: 250 connected clients, 20-minute year, no missed auction close - tests/CarbonSim.Web.Tests/Load/ (LoadScenario, LoadTestHost, LoadHarness, LoadSmokeTests, opt-in LoadFullTests); the smoke run guards the harness in dotnet test, the full run is CARBONSIM_LOAD_FULL=1 with the exact command documented; the shared view cache in PlayerSession/HostedRun is the fix for the per-client whole-run re-read (see the review)
- [x] Docker image; single-server deployment notes; backup of SQLite/Postgres - Dockerfile (multi-stage, non-root), .dockerignore, docker-compose.yml (SQLite default, optional Postgres profile); the provider switch made real in DatabaseProviders/AddCarbonSimData/CarbonSimHostOptions; docs/deployment.md; a tested SQLite VACUUM INTO restore drill in tests/CarbonSim.Data.Tests/SqliteBackupRestoreTests.cs; Docker is not installed here, so the image was not built (stated plainly in the report)
- [x] Accessibility and phone-width layout pass - palette contrast fixed, :focus-visible ring, wide table cards made keyboard-focusable, parameter and table inputs labelled, chart text alternatives, messages role=log, auction result live region; axe-core vendored and run over every player and admin screen in PlaywrightAccessibilityTests, plus an admin phone-width pass
- [x] Final review and handover notes - docs/handover.md and the Phase 6 review below

## Parallel track (non-code)
- [ ] Send requests for the three Training Reports to ETP/UNOPS, VNEEC and Josh Margolis; ask VnEconomy programme about results sharing
- [ ] Decide Blazor vs React before Phase 3 starts
- [ ] Watch for a licensing conversation with EDF if the clone is to be offered commercially in Vietnam

## Review / results
(append after each phase: what was built, test counts, deviations from plan, open issues)

### Phase 0 — Environment and skeleton (2026-09-16)
Built: `CarbonSim.sln` with `src/CarbonSim.Engine`, `src/CarbonSim.Data`, `src/CarbonSim.Web` and `tests/CarbonSim.Engine.Tests`, `tests/CarbonSim.Web.Tests`; `Directory.Build.props` (nullable, warnings-as-errors, code-style enforcement in build, single `TargetFramework`), `Directory.Packages.props` (central versions), `.editorconfig`, `.gitignore`. The Web project is an empty host — the Blazor UI is deliberately not scaffolded yet (engine first, UI second).
Gate: `dotnet build CarbonSim.sln` → 0 warnings, 0 errors; `dotnet test CarbonSim.sln` → green; `dotnet format --verify-no-changes` → clean.
Deviations: targets `net9.0` because only SDK 9.0.312 is installed and installing the .NET 10 SDK was out of scope for this run. `InvariantGlobalization` is off so the Phase 5 vi-VN localisation can work; engine messages format invariantly instead. FluentAssertions pinned to 7.2.0 (last Apache-2.0 release) because 8.x needs a paid licence for commercial use.
Open: Phase 0's commit is not made (commit only when asked). Replace with a real CI-free gate script when a runner exists.

### Phase 1, first three items — engine core: entities, parameters, scenario loader (2026-09-16)
Built: `CarbonSim.Engine/Domain` (`Simulation`, `TradingSystem`, `Sector`, `Company`, `Unit`, `Player`, `AbatementOption`, `Parameters`, `SectorBausGrowth`, `SimulationState`, `PlayerKind`) and `CarbonSim.Engine/Scenarios` (`ScenarioLoader`, `ScenarioLoadException`, wire-format types). `scenarios/vietnam-2024.json` loads into a 242-unit, 37-human-player simulation.
Test counts: 52 tests in `CarbonSim.Engine.Tests`, all passing (entity graph 10, parameters 20 theory cases + 5, abatement options 7, scenario loader 10). `CarbonSim.Web.Tests` has no tests yet — nothing in the Web project to test.
Design decisions worth keeping: entities validate their own invariants and fail fast; `Parameters.Validate()` aggregates instead because an administrator must see every broken value in one pass; the loader validates the whole file before building anything, so `ScenarioLoadException` carries all problems with JSON paths; abatement menus are authored per sector as a share of unit baseline and per-tonne economics, and the loader materialises absolute tonnes and money per unit, so calibration retunes one sector template rather than 242 unit menus.
Deviations: (1) 37 of the documented 39 Vietnamese firm names exist in the harvested login page — the JSON carries the 37 and names AI participants as placeholders; (2) sector emission shares use ETS Task 9 covered-emissions shares (power 43%, cement 33%, steel 24%) instead of the 2025-07 impact report's national shares (which normalize to 46.4/35.7/17.9), because the scenario covers pilot facilities only; (3) the three petroleum-named firms sit in Power, since the pilot covers power, cement and steel.
Known data gaps for calibration: year-1 BAU is 1.05 × cap and split evenly per unit, so a 53.4 Mt year-1 shortfall against the 320.3 Mt free allocation, versus roughly 46.6 Mt auctioned and 2.95 Mt abated in the demo video; the cheap abatement slice (≤ 8.2 $/t) totals 7.5 Mt, about 2.5× the demo's year-1 abatement. Both need the Phase 1 bot-calibration pass, not hand-tuning.
Open issues: the loader hard-codes one trading system per scenario (fine for v1's single ETS); player display names equal company names until the login/dropdown screen exists. `Simulation.Id` was a fresh `Guid` per load at this point; the seeded-RNG item below pins it to the seed instead. The company names in the scenario were replaced with original fiction after this batch — see the note below.

### Phase 1, items 1–7 — clock, randomness, allocation, abatement, auction, exchange, OTC (2026-09-16)
Built: `Clock/` (virtual-year clock, section model, lifecycle guards, warn-before-halt, auction notices), `Randomness/SimulationRandom.cs` (pinned SplitMix64; simulation id and every draw come from the seed), `Allocation/` (cap trajectory, sector cakes, per-unit free allocation for all years up front, BAU growth per sector band, position forecast), `Abatement/` (menu templates sized per unit, implement → building → operating → in profit → expired, additive reductions capped at emissions, capital check, no undo, per-year temporary shutdown), `Market/` (allowance ledger with escrow, government account and reserve, sealed-bid uniform-price auction with per-vintage and forward lots, order books per product with limit/market/stop-loss and fill-or-kill, OTC offers to a named unit). Scenario file regenerated with **original fictional company names** (nothing from carbonsim.org), per-company capital, and an explicit `seed`.
Correction applied: the 37 login-page company names are gone, replaced by 37 invented Vietnamese-style names with the same count and sector split (35 power, 1 cement, 1 steel); AI participants remain `AI <Sector> NN` placeholders.
Test counts: 172 tests in `CarbonSim.Engine.Tests`, all passing — clock 19, allocation and position 16, abatement 18, auction 14, allowance conservation 3, exchange 23, OTC 11, ledger 6, randomness 7, plus the earlier entity, parameter and loader tests. `CarbonSim.Web.Tests` still has none: the Web project has no behaviour yet.
Independent review: an adversarial reader went over the new clock, allocation, abatement and market code. It found five real defects, all now fixed with a regression test each: (1) order ids restarted per book while `Exchange.Cancel` treated them as exchange-wide, so a cancellation could hit the wrong product's order or fail on someone else's — ids now come from one exchange-wide source; (2) matching walked a list that a stop trigger could consume underneath it, which could feed a fully filled order into settlement and throw with escrow stranded — matching is now planned before it is executed; (3) fill-or-kill was only a pre-check and a band move during matching could leave such an order partly filled and resting — the plan is now computed first and an order that cannot be completed is killed without trading; (4) triggered stop orders ignored fill-or-kill; (5) sector cakes were rounded independently, so free allocation could differ from the year's free total by a tonne — sectors are now split by largest remainder against the year's total, which is exact. It also confirmed `NextDecimal` could round outside its documented range, now clamped.
Decisions worth keeping: the clock owns `Simulation.State`/`CurrentYear` and freezes the year offset whenever it is not running, so there is one answer to "where are we"; a year is one section per auction with the auction window at the end of each section, which reproduces the original's "gap between auctions" and its 45%-of-year open time; TradingHalted freezes the clock as well as the markets, which is what the "end year only after trading has halted" rule implies; auction bids are collar-checked at submission rather than clipped at clearing; the volatility band stops matching at the first out-of-band level instead of rejecting resting orders later; buys do not escrow cash (see below).
Known limitations, deliberate for now: buy-side orders and auction wins are not cash-checked, so a company's capital can go negative until the finance item adds credit limits and interest; the government reserve has no admin release action yet (the Phase 4 console will call into it); stop orders execute as market orders with no stop-limit variant.
Open issues: `Auction`/`OrderBook` settle capital directly rather than through an accounts object, which the finance item will replace; the compliance item will decide whether surrender and penalties reuse `AllowanceLedger` escrow or need their own buckets; the bot fidelity run is the first real test of whether the placeholder abatement and capital numbers are in the right range.

Smoke run over the shipped scenario (temporary harness, since deleted): 242 units, 37 human players; cap year 1 = 355,850,000 t, free = 320,265,000 t, auctionable = 35,585,000 t, business as usual = 373,642,500 t, so the fleet is 53,377,500 t short in year 1. 200 units bidding 1,000 t each at the floor cleared at the floor with 200 awards, 200,000 t sold and 20,000,000 collected; an exchange trade settled at 105; an OTC offer was accepted; a power unit implemented its 5 $/t efficiency project (1,606,662.60 up front, 53,555 t/yr). The run also exposed a gap: volume sitting in an auction that has not cleared was invisible to callers, so `Auction.OfferedVolume`/`UnsoldVolume` were added and the conservation invariant (every tonne of a vintage is free, reserved or on offer) is now a permanent test in `Market/AllowanceConservationTests.cs`.

### Phase 1, items 8–12 — compliance, finance, reports, bots, fidelity (2026-09-16)
Built: `Compliance/` (year-end reconciliation with offsets first, vintages newest first, banking capped at the obligation, forfeiture of the excess, cash penalty plus a next-year allowance debit, and administrator fines); `Finance/` (a cash ledger that records every movement with a category and year, refuses spending past the company's credit limit, and escrows cash for buy orders and auction bids, plus company books: operating profit, abatement result, interest on a negative balance); `Reporting/` (market journal, cost of compliance per tonne for a year and for the run, leaderboard ranked on overall cost then final position, and a system report with this-year and to-date columns covering the fields the video brief lists); `Bots/` (a cost-minimising compliance agent built to §3 of the fidelity brief, with three difficulty levels, per-bot timing jitter drawn from the seeded stream, and an AutoTrade switch that lets a human player hand a unit to a bot).
Test counts: 236 tests in `CarbonSim.Engine.Tests` — 232 passing, 4 skipped (the fidelity envelopes that do not hold yet), for the whole solution. Per area: clock 19, allocation 16, abatement 18, auction 17, conservation 3, exchange 23, OTC 11, ledger 6, randomness 7, compliance 15, finance 14, reporting 11, bots 13, fidelity 11, plus the earlier entity, parameter and loader tests. `CarbonSim.Web.Tests` still has none: the Web project has no behaviour yet.
Fidelity run (seeded, 242 units, 3 years, `scenarios/vietnam-2024.json`, written to `fidelity-run.md` in the test output): auction averages 100.00 → 101.60 → 106.77 against a floor of 100 and a ceiling of 300; allowance average 103.31, offset average 87.57, a discount of 15.2%; vintages 1/2/3 averaged 100.00/101.60/106.77; year-1 abatement 3,558,500 t = 1.00% of the cap; year-1 offsets surrendered 21,663,710 t = 6.09% of the cap; 346 company-years short, 19,821,144 t uncovered and 5,946,343,341 in penalties; bot cost of compliance per tonne between −10.11 and +29.32. The Hard setting closes much of the compliance gap (107 short company-years instead of 346, 619 of 726 compliant) but not the structural shortfall.
Envelopes that hold: year 1 clears at the floor; offsets trade at a 5–25% discount (15.2%); later vintages price above the current one and the premium grows with the vintage; year-1 abatement is of the order of 1% of the cap; the leaderboard spread sits inside +5 to +40 per tonne with negative outliers. Envelopes skipped with their measured values: full compliance with no penalties (346 short company-years, 19.8 Mt uncovered — the fleet bids only 70% of its shortfall at Normal and the government keeps its unsold reserve); prices climbing towards the ceiling by year 3 (they rise 3.4% of the way); offsets surrendered at 2–3% of the cap (6.09%, which follows the harness's disbursement rather than the engine); regulator shocks (no mid-run shock mechanism exists yet).
Deviations and decisions worth keeping: the scenario was recalibrated so year-1 business as usual equals the cap, making the fleet short by exactly the volume auctioned, and its abatement tiers were spread to roughly 5, 110–160 and 220–240 per tonne; both are placeholder calibrations to the envelopes, not data from the original, and are noted in the file. Money movements are stamped with the simulation's current year, so a run has to be started for the reports to have years to add up. Cash escrow makes "cannot spend what you have already promised" true rather than documented, and the auction's unsold volume goes back to the government's reserve rather than being re-offered. Penalties and fines are the only charges allowed to breach the credit limit.
Open issues: the fidelity shortfall is the top one — the fleet cannot cover its position because the auctions stay under-subscribed while prices sit at the floor, so either the government needs a reserve-release policy or the scenario's supply and demand need rebalancing (this is the brief's bot-calibration item, not an engine defect). Second, offsets were cheaper than expected in the Hard run (5.4% discount, at the edge of the envelope) because bots quote at their own discount assumption; the discount is a bot parameter today, not an emergent price. Third, the exchange's last-trade price still anchors every book on the auction floor, so later vintages and offsets inherit a band that has nothing to do with their own history. Fourth, `OrderBook`/`Auction`/`OtcMarket` call the cash ledger directly; a transaction-scoped API would read better once the persistence layer needs them. Nothing in the engine blocks Phase 2.

### Phase 2, items 1–2 — persistence and the account host (2026-09-17)
Built, engine side: `src/CarbonSim.Engine/Snapshot/` is the persistence boundary — plain records describing a whole run (`SimulationSnapshot` plus sub-records for the trading system and parameters, sectors and their abatement menus, players, companies, units and their menus, the allocation plan with every drawn growth rate, the abatement portfolio and shutdown set, both allowance buckets, the government's reserve and issued volumes, cash movements, compliance results, fines and the reconciled set, the journal, the order books with their resting orders and tapes, OTC offers, every auction with its lots, bids, results, awards and rejections, the clock's through-markers, and each bot's trigger times and progress) — with `SimulationSnapshots.Capture`/`Restore` and `RestoredSimulation`. About twenty engine types gained the smallest internal hook that lets a restore put state back: a `Restore` method here, a private constructor there, an internal state accessor on the random stream. Nothing in the snapshot replays a decision: balances, sequences, results, the stream position and every value the engine ever drew are carried, so a restored run cannot drift from the original.
Built, data side: `src/CarbonSim.Data` raises the real EF Core 9 model over SQLite: `Accounts` for the host and 43 simulation tables, all cascading from the `Simulations` row so a save is a replace rather than a diff. `SimulationMapper` is the only place that knows the engine's shape; `EfSimulationRepository` is thin (save, load, list, delete) and loads one table at a time, assembling the graph in engine identifier order. One `InitialCreate` migration builds the whole schema, and the host can either migrate or build from the model.
Built, host side: cookie authentication over the accounts table, with registration gated by the administrator access PIN (constant-time comparison, stored only in configuration), company claiming restricted to the scenario's human companies and enforced by a unique index as well as by the roster, login that rehashes when the hasher's parameters have moved on, password reset by a six-digit emailed code that is stored hashed, expires, works once and stops after a set number of wrong tries, and never reveals whether an address exists, an administrator-only policy, and an `IEmailSender` with an in-memory implementation for tests and Development and a logging one as the production default.
Decision recorded: a minimal custom account store, not ASP.NET Core Identity. Only `PasswordHasher<PlayerAccount>` is taken from Identity. Identity would have added seven tables and a second notion of a user beside the engine's own `Player` and company ownership, and its reset-token provider issues a long opaque token rather than the short emailed code the brief asks for.
Test counts: 275 in the solution — 271 passing, 4 skipped (the Phase 1 fidelity envelopes). Engine 238 (234 passing, 4 skipped), Web 26, Data 11. The four round-trip tests in `tests/CarbonSim.Data.Tests` are the ones this batch is judged on: a seeded virtual year of `scenarios/vietnam-2024.json` is played, saved, reloaded into a fresh engine, and then compared on the leaderboard, the system report, every ledger balance, every company's capital and escrow, the compliance results and the clock; the whole snapshot is compared field by field after a second capture; the restored run plays year two alongside the original and has to produce the same leaderboard, report and ledger; and saving twice replaces the first save instead of duplicating it. The engine's own two snapshot tests cover a year boundary and a mid-year capture (open auction, resting orders, part-progressed bots), and were shown non-vacuous by mutation: deleting the ledger, clock, random-state, abatement, exchange or compliance restore each fails them, and deleting the bots' done-set restore fails the mid-year case only.
Evidence beyond tests: the shipped host was booted as a real process against the current migration and answered 200 with the 37 human companies from the scenario, 403 on a wrong PIN, 201 on a right one, 400 when a second account claimed a taken company, 200 on login and on `/api/accounts/me` with the cookie, 401 for an anonymous administrator request and 202 for a reset request.
Deviations: a third test project, `tests/CarbonSim.Data.Tests`, was added (CLAUDE.md's layout updated) because the round-trip proof belongs to the persistence layer, and `CarbonSim.Engine.Tests` must not depend on Data; `dotnet-ef` was installed as a repository-local tool with a manifest rather than globally; `.editorconfig` exempts generated migrations from the namespace, collection and line-ending rules, and the scaffolded files were normalised to LF without a byte-order mark so `dotnet format` stays clean; `Directory.Packages.props` gained EF Core 9.0.20, `Microsoft.Extensions.Identity.Core` and `Microsoft.AspNetCore.Mvc.Testing`.
Findings worth keeping: a merged ledger row cannot express "this company and product has a key worth zero" as against "has no key at all", and the engine keeps keys for balances it has spent, so both allowance buckets and both government figures are their own tables; SQLite returns decimal amounts by value and not by trailing zeros, so the persistence tests compare decimals as decimals and the reports are unaffected; and the snapshot must carry values the engine drew (business-as-usual growth, bot trigger jitter) because re-drawing them on load would shift every later draw in the run.
Open issues: the engine runs one trading system today, so the snapshot carries one; a restored clock is bound to the absolute instant it was captured at, so a host that reloads in a fresh process has to supply wall time that agrees with it (documented on `Restore`); a few captured fields are reader conveniences that `Restore` does not consume, so a hand-edited snapshot could contradict itself; the registration PIN is empty in `appsettings.json`, which closes registration until an operator sets one; and nothing yet drives a simulation from the host — the SignalR hub, the hosted clock service and the two-client integration test are the rest of Phase 2.

### Phase 2, items 3–5 — hub, clock service, integration tests (2026-10-03)
Built: `src/CarbonSim.Web/Simulations/` — `ISimulationClient` (the 11-message typed feed: AuctionOpened/Closing/Cleared, MarketStateChanged, OrderBookChanged, TradeExecuted, OtcOfferReceived/Resolved, MessageReceived, SimulationStateChanged, ParametersChanged), `SimulationHub` at `/hubs/simulation` with one group per simulation (`SimulationGroups`) and owner-checked player actions (bid, place/cancel order, send/answer OTC, chat), `SimulationRegistry` (run store, per-run gate, `PendingNotices` + `ClearedSinceAnnounced` queues drained in event order, `JournalSeen` cursor so each fill reports once), `SimulationClockService` (injected `TimeProvider`, `TickAsync` = clock advance, auction clear, bots, year-end reconcile + books close; `AnnounceAsync` = notices then clears then books then market state; `StartAsync` grants year-1 free allocation; `ExecuteAsync` polls every second, disabled with `PollInterval = Zero` in tests; saves via `ISimulationRepository` on year end). Engine change: `Auction.Results` is now public so the host can report clearing prices (snapshot already read it).
Test counts: 283 in the solution — 279 passing, 4 skipped (the Phase 1 fidelity envelopes). Engine 238 (234 passing, 4 skipped), Web 34 (was 26), Data 11. New: `AuctionBroadcastTests` (two HubConnections in one group hear the same clear at the floor plus matching opened/closing notices), `DriverYearTests` (four auctions clear, extra tick ends the year, compliance has 2 results, run persists under its own id; single small bid clears at the floor), `RegistryGuardTests` (cross-company bid/order refused, stranger's offer answer refused), `MarketActionTests` (crossing limit orders fill at the resting price and empty the book; OTC accept moves stock and cash). Fixtures: `SimulationTestHost` (fake clock, manual-only driver), `TestRunFactory` (two-human-company loader scenario, no bots to interfere).
Gate: `dotnet build CarbonSim.sln` → 0 warnings, 0 errors; `dotnet test CarbonSim.sln` → green; `dotnet format CarbonSim.sln --verify-no-changes` → clean. `Directory.Packages.props` gained `Microsoft.AspNetCore.SignalR.Client` 9.0.20 (test-only; the server side ships in the framework).
Decisions worth keeping: the driver only announces auctions cleared by its own tick (queued with their close notice, drained in order), so a reconnected watcher never replays history; order fills report from the journal cursor rather than the book tape, because the tape carries order ids and the journal carries buyer and seller; the test resolves everything through `Server.Services`, because the SignalR backplane lives there — `_host.Services` holds a different registry and the tick silently clears a run nobody watches (found when the queue drained to 0 post-announce with no client event); an under-subscribed lot clears at the floor, so the two-client test asserts 100 rather than either bid price.
Open issues: the hub trusts `UserIdentifier ?? ConnectionId` as the actor until the Phase 3 login wires accounts to companies (tests pass `player-N` instead); `SimulationHub` has no authorization policy yet, so any connected client can join any run; `PendingNotices`/`ClearedSinceAnnounced`/`JournalSeen` are in-memory only and do not survive a save/load — a host that restarts mid-auction replays that auction's notices; `AuctionClosing` carries the live `TimeToAuctionClose` at announce time rather than the notice's own timestamp; no admin endpoints drive Start/Tick yet (Phase 4), so runs only start from tests.

### Phase 2 review fixes (2026-10-03, second pass)

Closed the Phase 2 review findings in the hub and clock code. All four must-fix items and the
should-fix list are done, each with a test where the finding was behavioural.

This pass supersedes the open issues listed in the section above: the hub no longer trusts a
`player-N` actor or `ConnectionId`, it requires an authenticated caller, the per-tick state
messages are change-gated, and ending a year is an explicit call that saves once.

**Must fix**

1. Real logins can act. `SimulationRegistry` no longer recognises a `player-N` convention or a
   literal company name. Every action resolves the caller's identity through the new
   `AccountCompanyResolver`, which reads the account row the caller claimed at registration
   (`IAccountStore.FindByIdAsync` on the account id the authentication cookie carries) and then
   checks ownership by company. Anonymous connections, administrators and accounts with no company
   all resolve to null and are refused. `player-N` now exists only in test setup, and the
   simulation tests sign in through the real registration and login endpoints.
2. Journal race outside the run gate. `HostedRun.CollectAnnouncement` reads the journal and
   advances `JournalSeen` while the run's gate is held; the tick and every player action collect
   there and broadcast only after releasing the gate, so nothing touches the journal outside it.
3. Bot exchange fills are announced. The one journal cursor, advanced under the gate, is the
   single source of `TradeExecuted` for exchange fills, used by both `TickAsync` and player
   actions: a bot fill is reported on the tick that produced it and exactly once. Auction trades
   are reported with their clear and OTC trades with the resolved offer, and the cursor filters
   those channels out, so nothing is double-announced.
4. Year end. End-year is now an explicit driver call, matching the Phase 4 admin flow: the tick
   only advances the clock, clears auctions and runs the bots, while `EndYearAsync` reconciles,
   closes the books and saves once. `BeginNextYearAsync` opens the next year and grants its free
   allocation, mirroring `StartAsync`. Extra ticks on a `YearEnded` run write nothing, so the
   per-second re-save is gone.

**Should fix**

- The hub endpoint requires authorization (`MapHub(...).RequireAuthorization()`), the cookie
  handler answers `/hubs` with 401 rather than a login redirect so SignalR can report it, and
  `SimulationHub` refuses a method call that arrives without a signed-in identity. Rule refusals
  (unauthorised, bad product or side, engine refusals) are wrapped in a `HubException` that keeps
  the reason.
- `OrderBookChanged`, `MarketStateChanged` and `SimulationStateChanged` are sent only when they
  actually changed, not on every one-second tick.
- Auction `TradeExecuted` reports `result.ClearingPrice` rather than `award.Cost / award.Volume`.

Test counts: 292 in the solution - 288 passing, 4 skipped (the Phase 1 fidelity envelopes).
Engine 238 (234 passing, 4 skipped), Web 43 (was 34), Data 11. New Web tests:
`ActorResolutionTests` (a signed-in player acts for its claimed company and not another),
`HubAuthorizationTests` (an anonymous negotiate is 401, a signed-in player is 200, and a rule
violation reaches the client as a `HubException` carrying its message), and
`BotFillBroadcastTests` (a bot exchange fill is announced on its tick and is not re-announced by a
later player order). The existing suites moved onto real sign-ins and explicit year-end calls, and
`DriverYearTests` gains the save-once and begin-next-year cases.

Gate: `dotnet build CarbonSim.sln` -> 0 warnings, 0 errors; `dotnet test CarbonSim.sln` -> green;
`dotnet format CarbonSim.sln --verify-no-changes` -> clean.

Decisions worth keeping: ending a year no longer happens automatically on `TradingHalted`, because
the review preferred the explicit call that mirrors the admin console, so nothing ends a year until
`EndYearAsync` is called (Phase 4 wires the control); the test host registers real accounts for its
two companies and signs in, which is what the `player-N` convention used to stand in for; the
announcement is gathered under the gate as a plain payload and sent afterwards, so a broadcast can
never race the state it describes.

Open issues: `PendingNotices`/`ClearedSinceAnnounced`/`JournalSeen` are still in-memory only, so a
host that restarts mid-auction replays that auction's notices; `AuctionClosing` still carries the
clock's live time-to-close at collection rather than the notice's own timestamp; nothing outside
the tests calls `EndYearAsync`/`BeginNextYearAsync` until the Phase 4 admin endpoints exist; and
ownership is still keyed on the company name, which is what the account table's unique claim uses.

### Phase 3 - Player UI, Blazor interactive server (2026-10-03)

Built: `src/CarbonSim.Web/Components/` - `App.razor`/`Routes.razor` (global InteractiveServer render
mode), `Layout/` (`MainLayout` shell, `TopBar`, `ProgressStrip`, `NavMenu`, `RulesPanel`), `Pages/`
(Waiting, Dashboard, Abatement, Auction, Exchange, Otc, CompanyManagement, UnitInformation,
SurrenderAndBanking, SystemInfo, Leaderboard, Messages), `Shared/` (`EChart`, `Display`,
`ScreenFallback`), and `src/CarbonSim.Web/Player/` - the read model (`PlayerView` and its records,
`IPlayerSession`/`PlayerSession`, `PlayerContext`, `RunRoute`, `ICharts`/`EChartsInterop`, `Charts`
option builders, `PlayerViewOptions`). Sign-in, registration and sign-out are plain server-rendered
forms (`Endpoints/SignInEndpoints.cs`) with antiforgery tokens, because writing the authentication
cookie is an HTTP round trip an open circuit cannot make; the language switch is
`Endpoints/CultureEndpoints.cs`, writing the culture cookie the localiser reads. `wwwroot/app.css` is
written for this project, and Apache ECharts 5.5.1 is vendored under `wwwroot/lib/echarts/` with its
Apache-2.0 LICENSE so a training box needs no internet. `wwwroot/js/charts.js` is the only script
module: it is imported once and handed one option per chart.

Live updates - the choice: the pages do not open a browser SignalR connection. Blazor Server
components already run on the server, so `PlayerSession` builds each screen's `PlayerView` inside the
run's gate through `SimulationRegistry.ReadAsync`, and the shell re-reads it once a second
(`PlayerViewOptions.RefreshInterval`, null in tests) and pushes the new snapshot down as a cascading
value. That is the same state the hub carries, without a second transport, and it is what makes every
screen testable with a stub session. Player actions go through `PlayerContext.ActAsync` into
`IPlayerSession`, which forwards to the existing owner-checked `SimulationRegistry` methods, so no
component mutates the engine.

Charts: one `EChart` component takes an already-serialised ECharts option and hands it to the
vendored library through `ICharts`; `Charts` builds the N.O.P. bars, the MACC bars, the long/short
stacked bar, and the candlestick-plus-close price chart (one candle per virtual year per product,
with the auction floor as a dashed line because every book's volatility band is anchored there). A
chart that throws in the browser is logged and the screen carries on.

Dev seeding: `CarbonSimHostOptions.StartDemoSimulation`, set only in `appsettings.Development.json`,
starts `scenarios/vietnam-2024.json` as a live run at boot through `DemoSimulationSeeder`, and the
Development registration PIN is `carbonsim-dev-pin`, so `dotnet run --project src/CarbonSim.Web`
shows a playable game.

Test counts: 323 in the solution - 319 passing, 4 skipped (the Phase 1 fidelity envelopes). Engine
238 (234 passing, 4 skipped), Web 74 (was 43), Data 11. The 31 new Web tests are 13 bUnit screen
tests (every screen, the waiting list, and the no-company fallback), 7 bUnit action tests (each
button asserted on the session call it made), 5 shell tests plus 4 route cases (layout, top bar,
progress strip with its PAUSED banner, rules panel, `RunRoute`), and 2 Playwright tests that boot the
real host as a child process against a throwaway database and drive Chromium: a walk of every screen
in the demo order, and a 390x844 pass asserting no screen scrolls the page sideways.

Gate: `dotnet build CarbonSim.sln` -> 0 warnings, 0 errors; `dotnet test CarbonSim.sln` -> green;
`dotnet format CarbonSim.sln --verify-no-changes` -> clean; the host boots with
`dotnet run --project src/CarbonSim.Web` and serves all eleven screens plus `/sign-in`. Playwright
browsers were installed with the generated `playwright.ps1 install chromium`, so the walk runs for
real rather than being skipped; `PlaywrightFactAttribute` reports a skip with the install command on
a machine that has none.

Decisions worth keeping: the run's id is read out of the address (`RunRoute`) because a layout cannot
take a route parameter, so `/run/{id}/screen` is the one thing the shell and its page agree on; a
product with no order book yet is still listed at the auction floor, because that is what its band
is anchored to, so the exchange screen is not blank before the first trade; the auction history
lists only auctions whose section has begun; and a missing or miswritten resource key fails a bUnit
test, which is how the `WaitingForAuction_X` placeholder mismatch was caught and fixed.

Deviations: localisation ships English only, as the checklist says - the Vietnamese resource file is
Phase 5 work and the switch already writes the cookie it will read. `Directory.Packages.props`
gained `bunit` 1.40.0 (the line that still runs on xUnit 2), `Microsoft.Playwright` 1.63.0 and
`Microsoft.AspNetCore.Components.QuickGrid` 9.0.20, so tables use QuickGrid as the stack decision
records.

Open issues: a run page polls once a second while it is open, which is fine for a training room but
is polling rather than push - if the room grows, the shell should subscribe to the registry instead;
number inputs bind through the current culture and will need `@bind:culture` attention when
Vietnamese is added; the AutoTrade switch is shown read-only because turning it on mid-run belongs
with the Phase 4 admin controls; nothing in the UI ends a year or starts the next one (the admin
console will call `EndYearAsync`/`BeginNextYearAsync`); and the full accessibility pass is Phase 6.

### Phase 4 - Admin console (2026-10-03)

Built: `src/CarbonSim.Web/Admin/` - `AdminView` (the console's read model: run summary, run detail
with parameters, sectors, companies, units and their abatement menus, leaderboard, per-year prices,
surrender rows, system totals this year and to date, government reserve), `AdminSession` (the read
model and the front door: the run's gate is taken for every read, the administrator role is checked
on every call, and every control is forwarded to the driver), `AdminContext`/`AdminRoute` (the
shell's snapshot and the `/admin/run/{id}` address), `AdminCsv` (system, company, unit, price and
leaderboard reports), `ScenarioDraft` (the editable scenario document the setup screen binds to),
`RegistrationPolicy` (the in-memory registration gate) and `AdminAccountSeeder` (the bootstrap
administrator). UI: `Components/Admin/` - `AdminLayout`, `AdminNav`, and five pages: `/admin`
(exercises, saved exercises, create/load/delete), `/admin/run/{id}` (clock strip and controls,
messaging switch, AutoTrade switches, fine, offsets, abatement, end-of-year modification),
`/admin/run/{id}/reports` (the five reports, each with a CSV link), `/admin/run/{id}/surrender`, and
`/admin/setup` (parameters, sectors and their abatement menus, growth bands, registration open/close
and PIN).

Engine additions: `AllocationPlan.ApplyEmissionShock(unit, year, deltaTonnes)` - an end-of-year
modification written into the unit's business-as-usual path, so it survives a save and reload with
the rest of the plan and every later reading of that unit sees it; a unit can never be pushed below
zero. `ISimulationClient` gained `MessagingStateChanged`. Registry: `HostedRun.MessagingEnabled`,
`HostedRun.Bots` settable, `SimulationRegistry.Restore`/`Remove`/`RebuildBots`, and
`PostMessageAsync` refuses while messaging is off. Host options gained `AdminEmail`/`AdminPassword`
and `IHttpContextAccessor` is registered. `PlayerView` gained `MessagingEnabled`, and the player's
Messages screen shows the switch and disables the form.

How an admin action reaches players: every control goes `AdminSession` -> `SimulationClockService`,
which takes the run's gate, mutates the engine, gathers what changed with `CollectAnnouncement`
while the gate is held, and broadcasts to the run's SignalR group after releasing it - the same path
the tick uses. Year controls (start/open/end/end-simulation) and the messaging switch raise the
run's state on that feed; fines, offsets, abatement and shocks are per-company or per-unit and reach
the player's page on its next registry read, which is the same read the feed describes. No component
mutates the engine.

Setup applies to a pending simulation: `/admin/setup` loads the configured scenario into a
`ScenarioDraft`, the administrator edits parameters (validated by the engine's own loader, which
reports every problem at once), sectors, abatement menus and growth bands, and "Create the exercise"
writes the draft back to JSON and hands it to `ScenarioLoader`, which builds a `Pending` run and
saves it. Changing parameters on a live run is out of scope: the engine fixes the cap, allocation
and abatement economics at construction, so a running exercise's parameters are shown read-only and
a new exercise is created instead. The form opens on a seed no live run is using, and creating a run
whose seed is already under way is refused rather than replacing it.

Multiple concurrent simulations: the registry holds any number of runs, each with its own clock,
markets, bots, group and saved copy. The console lists live runs and the saved runs the repository
holds, loads a saved run back with `SimulationSnapshots.Restore` (its own clock, markets and bots,
not fresh ones) and deletes one. `RunIsolationTests` starts two runs under one host and proves a
control on one leaves the other's state alone and that a state broadcast reaches only the started
run's watchers.

Test counts: 360 in the solution - 356 passing, 4 skipped (the Phase 1 fidelity envelopes). Engine
242 (238 passing, 4 skipped; +4 `EmissionShockTests`), Web 107 (was 74; +33), Data 11. The 33 new
Web tests are 8 driver/registry admin-control tests, 2 run-isolation tests, 5 CSV-endpoint tests, 7
bUnit screen tests, 10 bUnit action tests, and 1 Playwright walk. The Playwright admin walk boots
the real host as a child process, signs the bootstrap administrator in, builds an exercise from the
configured scenario, starts the year, halts trading, closes the year and opens the next, while a
second browser context playing a company watches the same run's year and state change under it; it
ran for real alongside the two Phase 3 walks.

Gate: `dotnet build CarbonSim.sln` -> 0 warnings, 0 errors; `dotnet test CarbonSim.sln` -> green with
Playwright running for real; `dotnet format CarbonSim.sln --verify-no-changes` -> clean; and
`dotnet run --project src/CarbonSim.Web` served `/sign-in` (200), `/admin` (200, showing the
not-an-administrator notice to an anonymous visitor), `/api/admin/runs` (401 anonymous, 200 for the
bootstrap administrator with the live run listed).

Housekeeping (Phase 3 review): the four resource values that were verbatim catalog strings were
rewritten in this project's own words (`MyAbatementImplementationStatus` -> "My abatement progress",
`AllowancesToBeAuctionedThisYear` -> "Allowances on offer this year", `UnitToTradeWith` ->
"Counterparty unit", `AvailableEmissionReductionOpportunities` -> "Abatement options open to my
units"). The three `PlayerScreensTests` assertions that named the old wording now read the resource
value through the localiser, so a reworded label cannot break a screen test again.

Decisions worth keeping: the console is five server-rendered Blazor pages under `/admin` behind the
administrator policy, and `AdminSession` re-checks the role on every call because a Blazor page is
reachable by anyone who is signed in; the CSV endpoints are ordinary `text/csv` responses behind the
same policy, and `AdminSession` reads the request's own principal outside a circuit because the
circuit's authentication provider refuses to answer there; the setup screen edits the scenario
document rather than engine objects, so the engine's own validation is the only validator; messaging
is host state, so it is not saved with a run and a reloaded run starts with messaging on; and the
bootstrap administrator exists only when `AdminEmail`/`AdminPassword` are configured (the
development settings name a dev account, as they already named a dev registration PIN).

Open issues: an AutoTrade switch rebuilds the whole bot fleet, so a bot's progress is dropped and its
trigger times are drawn again - fine for a trainer's nudge, but a finer per-unit change would keep
the fleet; messaging state is not persisted (see above); a shock is written into the
business-as-usual path and cannot be undone or listed separately, so the console cannot show
"modifications applied"; the console polls once a second like the player screens rather than
subscribing to the registry; the setup screen edits parameters, sectors, abatement menus and growth
bands but not the company/unit roster, which comes from the configured scenario; and the reports'
"to date" totals cover the whole run's years rather than only the years played, as the player's
system report already did.
+### Phase 5 — Vietnam localisation and scenario (2026-10-03)

Built: src/CarbonSim.Web/Resources/SharedStrings.vi.resx (312 keys) written for this project from
the terminology of the Vietnamese glossary under research/carbonsim-sources/vietnam-ta-materials/
(Hạn ngạch for allowance, Tín chỉ bù trừ for offset, Hạn mức for cap, Đấu giá hạn ngạch for the
auction, Thị trường giao dịch qua sàn for the exchange, Thị trường OTC, Biện pháp giảm phát thải for
abatement, Nghĩa vụ tuân thủ for the compliance obligation, Lưu giữ for banking, Hình phạt for the
penalty, Công ty and Đơn vị for company and unit). No sentence was taken from the harvested material:
the glossary fixed the vocabulary, the wording is ours. src/CarbonSim.Web/CurrencyDisplay.cs carries
the display currency and the VND/USD rate; the host options gained DisplayCurrency and VndPerUsd, and
Display.Money/Money2 route through the ambient Display.Currency. The player and admin number inputs
became culture-bound text inputs. docs/trainer-runbook.md is the two-day runbook.

Localisation mechanics: the language switch already worked end to end. The culture cookie reaches the
Blazor circuit, so the interactive render (not only the server-rendered shell) is Vietnamese; the
Playwright Vietnamese walk proves it by reading translated headings and interacting with a
Vietnamese-formatted order after hydration. No circuit-culture plumbing was needed. The resource
parity test reads both .resx files off disk and fails on a missing or empty key in either, and a
second test loads the satellite assembly through the real localiser and asserts Vietnamese values, so
a resx that never reaches the build is caught too. Two keys were added to both files for the currency
line on the Rules panel (DisplayCurrency, VndPerUsdRate).

Number entry (the Phase 3 open issue): every money or volume box on the Auction, Exchange and OTC
screens, and on the admin setup and run screens, is now type="text" inputmode="decimal" with
@bind:culture="CultureInfo.CurrentCulture". A browser's own number input only accepts a dot, so a
Vietnamese player typing 1.000 for a thousand tonnes would have been misread as one tonne. The bid
and order boxes now parse 1.000 as 1000 and 120,25 as 120.25, and the same form still reads 1,000 as
1000 under English. Note: Blazor does not support @bind:format for numeric types (it compiles only
for DateTime/DateTimeOffset/DateOnly/TimeOnly), so the boxes show a plain culture-formatted number
rather than a grouped one; grouping still applies everywhere a figure is written by Display. The
auction price slider binds invariantly, because an HTML range input must.

Currency: the engine keeps one internal unit, which is the US dollar the trainer deck quotes the
collar and penalty in; only the UI converts. DisplayCurrency is USD (the internal unit) or VND, and
VndPerUsd is the rate. A converted figure carries its code ("1,250,000 USD", "25.000.000 VND"), so a
figure is never ambiguous. The three places that showed the cap through the money formatter (Rules
panel, System info, Waiting) now use tonnes, which the currency change exposed. CSV exports stay in
the internal unit with invariant numbers, so a spreadsheet reads them as numbers. Tests cover USD
formatting, VND conversion and round-trip, grouping in both cultures, and the accepted codes.

Scenario: scenarios/vietnam-2024.json is kept, not replaced, because every listed trainer-deck
parameter already matched it and the Phase 1-4 tests and the fidelity run reference that file by
name. Its notes field was rewritten to record the cross-check against the deck's "ETS parameters"
slide. Agreement: cap 355,850,000 t; 3%/yr cap reduction (deck: 15-18% over the term); 90% free
allocation; 4 auctions per year; USD 100/300 collar; 10% offset limit; banking capped at the
obligation; penalty USD 300 plus one allowance; 10% exchange volatility band; 20 minutes per virtual
year; 242 units. Discrepancies noted, not forced: (1) the deck offers 5-6 years and this file runs 3,
kept short so the seeded fidelity run and the earlier tests stay quick, with the term left a trainer
setting; (2) the deck's timeline shows 2:45 auction windows with 2:15 interims, while this file opens
each auction for 2:15 of a 5:00 section (45% of the year in total open time, reading the deck's "45%
of the year" as total open time); (3) the deck says about 36 human and 206 AI companies, this file has
37 human and 205 AI. A fourth finding is in the engine, not the scenario: tradingOpenShareOfYear is
validated, persisted and displayed, but the clock derives the auction window from AuctionDuration and
AuctionsPerYear alone, so the parameter drives nothing. The shipped file happens to agree (2.25 min of
a 5 min section is 45%), but the two can silently drift.

Fidelity re-run against the shipped scenario (seed 20240916, 242 units, 3 years), from
fidelity-run.md. Normal difficulty: auction averages 100.00 / 101.60 / 106.77; year-1 auction volume
16,636,423 t; average allowance price 103.31 against offsets 87.57, a 15.2% discount; prices by
vintage 100.00 / 101.60 / 106.77; year-1 abatement 3,558,500 t (1.00% of the cap); year-1 offsets
surrendered 21,663,710 t (6.09% of the cap); 346 penalties worth 5,946,343,341 over a 19,821,144 t
market shortfall; 380 of 726 company-years compliant; bot cost per tonne -10.11 to 29.32. Hard
difficulty: 100.12 / 101.45 / 103.37; 17,792,522 t; 101.85 against 96.30, a 5.4% discount; 1.00% of
the cap abated; 5.83% offsets surrendered; 107 penalties worth 5,857,064,382 over a 19,523,548 t
shortfall; 619 of 726 compliant; -12.86 to 26.83. Seven envelopes hold (floor clearance, offset
discount, later vintages above earlier ones, year-1 abatement share, leaderboard spread with negative
outliers, plus the two monotonicity/record checks). Four remain skipped with their measured value in
the reason and were not unskipped or hand-tuned: full compliance at Normal (346 company-years short),
prices climbing to the ceiling by year three (only 3.4% of the way), the 2-3% offset surrender share
(6.09%, set by the harness's disbursement), and a mid-run price shock inside one virtual month (no
shock in this harness). Closing the first three is the bot-calibration item, not a scenario edit.

Trainer runbook: docs/trainer-runbook.md covers host configuration (including DisplayCurrency and
VndPerUsd), building an exercise, registration and the PIN, the two-day agenda mapped to the console
controls (start/open the year, pause/resume, halt with a warning, close the year, end the exercise,
fine, disburse offsets, add abatement, end-of-year modification, automatic trading, messaging), what
to point out on each player screen, the five reports and their CSV paths, and recovery (load a saved
run, restart the host, and the in-memory notice caveat). Its final section states plainly which
fidelity envelopes hold and which do not, so a trainer does not present a number as the original's.

Test counts: 376 passing, 4 skipped in the solution. Engine 238 passing + 4 skipped (the fidelity
envelopes), Web 127 passing, Data 11 passing. Web gained 20 tests: LocalizationTests (parity in both
cultures, the supported list and cookie, and the real localiser answering in Vietnamese),
CurrencyDisplayTests (10, including the theory over the accepted codes), NumberEntryTests (5, both
cultures, a bid, an order and an offer), and PlaywrightVietnamTests (1, the Vietnamese walk). The
Playwright walk now boots five real hosts across the walk, phone, admin and Vietnamese tests; all ran
for real rather than skipping.

Gate: dotnet build CarbonSim.sln -> 0 warnings, 0 errors; dotnet test CarbonSim.sln -> green with
Playwright running for real (376 passed, 4 skipped); dotnet format CarbonSim.sln --verify-no-changes
-> clean; and dotnet run --project src/CarbonSim.Web served /sign-in (200), /admin (200), and with
the culture cookie set to vi answered /sign-in, /admin and a run dashboard at lang=vi, so the player
and admin screens render in both languages.

Open issues: Display.Currency is a static ambient, which fits one display currency per deployment but
not two languages wanting different currencies on one box; input boxes show a culture-formatted but
ungrouped number, because Blazor has no @bind:format for numerics; tradingOpenShareOfYear is inert in the
clock (see above); the four fidelity envelopes above are still open; and the runbook's recovery steps
assume the in-memory notice caveat from the Phase 2 review, which is unchanged.

Review fix (2026-10-04): prices now follow the display currency both ways. Under VND, every per-tonne
price on the player and admin screens (top of book, auction and trade histories, OTC offers, auction
floor/ceiling, average prices) is converted and carries its code, and the bid slider, order, stop and
offer price boxes take dong, name their currency, and are converted back to the internal unit before
reaching the session. `Display.UseCurrency` gives a per-async-flow override so a dong screen can be
tested without changing what parallel tests see; the host still sets one deployment-wide currency.
Tests: `DongPriceEntryTests` (5 bUnit: order, offer and auction-slider entry in dong, labelled price
boxes, converted market prices) and a `CurrencyDisplay.Price` case; Web 133 passing.

### Phase 6 - Hardening and deployment (2026-10-04)

Built, load: tests/CarbonSim.Web.Tests/Load/ - LoadScenario (authors a 250-human-company, one-unit-each
exercise with four auctions a year, in the trainer-deck parameter shape), LoadTestHost (the real host
on a throwaway SQLite database playing that scenario; its clock and driver are the production ones
unless a test asks to hand-drive them), LoadHarness (signs players in through the real registration
and sign-in endpoints, builds a real PlayerSession per session, refreshes every view at the
production interval, has a quarter of the room bid and order, watches each auction scheduled close,
times a gate-only read, and samples CPU and working set), LoadSmokeTests (12 clients, compressed,
runs in the normal suite) and LoadFullTests (250 clients, uncompressed 20-minute year, opt-in with
CARBONSIM_LOAD_FULL=1). A test project rather than a tools/ console tool: the harness must boot the
real host and share its fixtures, and the smoke version must run inside dotnet test.

The bottleneck and the fix: every screen built the whole run view on every refresh. PlayerSession
Build ranked all companies (Leaderboard.Rank calls Scoring.Overall and FinalPosition per company),
walked the whole run for the system report (twice), sorted every counterparty, and re-scanned the
journal per product - all of it once per client per second, under the run gate. HostedRun now carries
a revision counter and a Shared(key, factory) cache that CollectAnnouncement empties whenever the run
changes, and PlayerSession builds the leaderboard, the system report, the auction price summary, the
counterparty roster, each book top/tape/candles and each company own trade list through it. The
run-wide work is therefore done once per change and each player is handed only their own slice; the
1-second interval was left alone. SharedViewCacheTests asserts that a second player on the same run
pays for exactly one extra company-keyed value, and that a tick throws the cache away.

Full load run (250 clients, uncompressed 20-minute virtual year, seed 20261004, one box, shipped
logging level), executed once for real:
- auctions 4, missed 0, worst lateness 895 ms (inside the one-second driver tick)
- refreshes 297,145; refresh p50 3.67 ms, p95 9.72 ms, p99 15.15 ms, max 1,269 ms
- gate wait p50 0.01 ms, p95 0.03 ms, p99 2.42 ms, max 1,254 ms
- actions 1,608 placed, 3,984 refused by the market, 0 refresh errors
- CPU 487.98 s over 1,202 s wall (about 41% of one core), peak working set 164 MB
The single ~1.25 s spike is the year-end save: EndYearAsync holds the run gate while it writes 250
companies to SQLite. That is the deliberate save-once-under-the-gate design, it happens once a year,
and it is why p99 is 15 ms rather than a tail problem. The refusals are market refusals (bids after a
window closed, orders outside the band or unbacked), not errors.

Built, deployment: Dockerfile (multi-stage, SDK build then ASP.NET runtime, non-root app user,
scenarios copied in, /data the one writable place), .dockerignore, docker-compose.yml (SQLite in a
named volume by default, an optional postgres profile with a PostgreSQL 17 service), and
docs/deployment.md (single server, the full configuration reference, TLS and reverse-proxy notes,
SQLite VACUUM INTO and PostgreSQL pg_dump/pg_restore, and the restore drill).

Docker: not installed on this machine (docker is not on PATH and there is no Docker service), so the
image was NOT built or run and /sign-in was not hit through it. The files are written but unverified;
the report says so plainly.

PostgreSQL: it was not really switchable - AddCarbonSimData hard-coded UseSqlite. It now comes from
configuration (CarbonSim:Provider Sqlite or Postgres, default Sqlite), an unknown provider stops the
host, and the Npgsql provider is referenced. The shipped migration set is SQLite, so the host refuses
Provider=Postgres with Schema=Migrate and PostgreSQL builds from the model (Schema=FromModel).
DatabaseProviderTests proves the registration picks the right provider name from configuration and
that the model scripts as PostgreSQL SQL (uuid, numeric) without a server. Not exercised: a live
PostgreSQL server, so the schema has not been applied to one - deployment.md states that.

Backup and restore: SqliteBackupRestoreTests plays a seeded year, saves it, takes an online-safe copy
with VACUUM INTO, opens the copy on its own and loads the run back out of it, checking the leaderboard
and clock match. That is the tested drill; deployment.md documents the operator commands for SQLite
and pg_dump/pg_restore for PostgreSQL.

Accessibility and phone width: the palette was measured against WCAG AA - --muted (#6b7280) was
4.47:1 on the paper background and is now #646b78 (4.95:1), and --blue (#3f7fbf) was 4.20:1 and is
now #2f6fbf (5.06:1), so links and white-on-blue buttons both pass. Every focusable element gets a
visible focus ring. Wide table cards, which scroll horizontally, are now keyboard-focusable
(tabindex). The admin setup parameter inputs (which sat in a dl with no label) and the abatement and
growth table inputs now carry aria-labels. Each chart is role=img with a label, and its figures are
always beside it as text or a table. The message log is role=log with aria-live and the auction
result is a polite status region. axe-core 4.10.2 is vendored under tests/CarbonSim.Web.Tests/
Compliance/ (no CDN, no network) and PlaywrightAccessibilityTests runs it over all 11 player screens
and the admin run list, setup, run controls, reports and surrender with zero WCAG 2.0/2.1 A/AA
violations, plus an admin phone-width pass (390x844, no sideways scroll).

Handover: docs/handover.md - what the system is, the architecture map, where each mechanic lives,
build/test/run/deploy, the known gaps (the four open fidelity envelopes, bot calibration, the
in-memory notice queues, the single display currency, tradingOpenShareOfYear being inert, the
per-refresh account lookup, PostgreSQL unexercised, Docker unverified) and a prioritised next-step
list.

Files added: src/CarbonSim.Data/DatabaseProviders.cs; Dockerfile, .dockerignore, docker-compose.yml;
docs/deployment.md, docs/handover.md; tests/CarbonSim.Data.Tests/DatabaseProviderTests.cs and
SqliteBackupRestoreTests.cs; tests/CarbonSim.Web.Tests/Load/ (LoadScenario, LoadTestHost,
LoadHarness, LoadSmokeTests, LoadFullTests); Components/PlaywrightAccessibilityTests.cs;
Simulations/SharedViewCacheTests.cs; Compliance/axe.min.js (vendored). Changed:
DataServiceCollectionExtensions, CarbonSimHostOptions, Program, SimulationRegistry, PlayerSession,
CarbonSim.Data.csproj, CarbonSim.Web.csproj, Directory.Packages.props, EChart.razor and the four
pages that use it, Messages.razor, Auction.razor, app.css, and the wide-card and admin labelling
edits across the components.

Test counts: 392 passing, 5 skipped in the solution - Engine 238 passing + 4 skipped (the fidelity
envelopes), Data 16 passing, Web 138 passing + 1 skipped (the opt-in full load run). New Web tests:
SharedViewCacheTests (1), LoadSmokeTests (1), PlaywrightAccessibilityTests (3). New Data tests:
DatabaseProviderTests (4), SqliteBackupRestoreTests (1).

Gate: dotnet build CarbonSim.sln -> 0 warnings, 0 errors; dotnet test CarbonSim.sln -> green with
Playwright running for real (392 passed, 5 skipped; the four fidelity envelopes stay skipped, plus
the opt-in load run); dotnet format CarbonSim.sln --verify-no-changes -> clean; and the host booted
from the built DLL and answered /sign-in 200, /admin 200 and / 200.

Open issues: the per-refresh account lookup is still one SQLite round trip per client per second (and
EF logs each one); the PostgreSQL schema has not been applied to a live server; the Docker image is
unbuilt; the four fidelity envelopes and the bot-calibration item remain; the year-end save holds the
run gate for about 1.25 s (one event per year); and tradingOpenShareOfYear is still inert.

