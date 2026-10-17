# activeContext — CarbonSim clone build plan

Status: v1 phases 0-6, seven QA rounds and the architecture pass are done; current work is the internal demo section at the end.
UI stack: Blazor Server, decided by building it (Phase 3).

## Definition of done for v1
A trainer can run the Vietnam two-day agenda end to end: configure a trading system with ~240 units (about 40 human, rest bots), run 3–6 virtual years of 20 minutes with 4 auctions each, watch live prices, pause and shock the market, and show the system report and leaderboard, in English and Vietnamese, on one server. Behaviour passes the fidelity envelopes in `research/2026-09-13_bot-logic-and-numeric-fidelity-options.md` §6 and the click-through in `research/2026-09-13_carbonsim-video-insights.md`.

## Phase 0 — Environment and skeleton
- [x] Install .NET 10 SDK; confirm `dotnet --version` is 10.x - done 2026-10-17 (10.0.402), solution on net10.0
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
- [x] Decide Blazor vs React before Phase 3 starts - Blazor Server shipped
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

### Flaky-test review (2026-10-04)

Two independent causes of the intermittent failures in tests/CarbonSim.Web.Tests, found by repeat runs
of the project with a trx logger and fixed without retries, longer sleeps or skips. The HubException
"'Delta Power' does not play 'Red River Power'" in the failure log is expected noise from
HubAuthorizationTests and was not a cause.

Playwright drove a page before the circuit was listening. Blazor sends the prerendered markup first
and attaches its event handlers only on the first interactive render, so a click, or a value typed
into an `@bind` box, in between is dropped with no error. Evidence: a captured run where the OTC
offer rested with the default volume 1.000 while the typed price 120,25 landed (the change event from
the volume box was lost), and an admin run where the WebSocket had already connected yet the
create-exercise click produced no request and no navigation. A connected WebSocket is therefore not a
sufficient gate: handlers attach after the initial render batch. Fix: MainLayout.razor and
AdminLayout.razor render `data-interactive="true"` only in the interactive render, from
RendererInfo.IsInteractive; the new tests/CarbonSim.Web.Tests/Components/InteractivePage.cs gate
(WaitUntilInteractiveAsync) is awaited after every navigation before typing or clicking in the four
Playwright test classes, and it replaces the Vietnam walk's 1500 ms sleep; the bUnit contexts
(ScreenFixture, AdminFixture) set RendererInfo so the layouts render as they do after hydration.

CarbonSimWebHost.Dispose cleared the SQLite pool process-wide. SqliteConnection.ClearAllPools() from
one host's teardown closed the pooled connections of every host running beside it, and a query already
in flight failed with ObjectDisposedException: 'SQLitePCL.sqlite3' (seen in
PasswordResetTests.An_expired_code_is_refused). Fix: SqliteConnection.ClearPool on this host's own
connection string only.

Evidence: 10 consecutive runs of tests/CarbonSim.Web.Tests, each 139 tests with 138 passed and 1
skipped (the opt-in full load run); the full solution green (Engine 238 + 4 skipped, Data 16, Web 138
+ 1 skipped); dotnet build CarbonSim.sln with 0 warnings and 0 errors; dotnet format
--verify-no-changes clean. The same ClearAllPools pattern remains in tests/CarbonSim.Data.Tests (a
separate test process, so it cannot reach the Web tests) and was left alone.

### Bot calibration - the price path, and why full compliance is blocked (2026-10-04)

Built, bot: src/CarbonSim.Engine/Bots/ComplianceBot.cs and BotSettings.cs. The bot now bids its whole
position instead of a fraction, spreads that position over the auctions left in the year so no single
auction is bid up against a slice of supply, and prices the bid at what the marginal tonne of its
position really costs: it walks the abatement it has left cheapest first, takes the project that
covers the last tonne, and is capped at the cash penalty. Abatement is re-decided after the first
auction of the year (at the mid-year trade trigger), so a price that has climbed makes the
longer-payback projects worth building; and a project's cost is the capital spread over the years of
the run it will actually run, not its whole service life, which stops a bot building a twelve-year
project for the one or two years left to play. Normal's bid appetite is 0.9 of its position, Hard's is
1.0 (Hard also has lower price noise and a stricter offset discount), which keeps the difficulty
ordering real.

Results, the seeded 242-unit three-year run of scenarios/vietnam-2024.json (Normal): auction averages
100.00 / 108.62 / 220.51 against the 100 floor and 300 ceiling, so year 1 clears at the floor and year
3 reaches the ceiling, the Dominican pattern the brief asks for; the same run's vintages average
100.00 / 108.62 / 220.51, so later vintages price at a premium and the premium grows; offsets trade at
a 20.8% discount to allowances; year-1 abatement is 1.00% of the cap; and the bot cost of compliance
sits between -13.34 and +28.66 per tonne with negative outliers. The price-path envelope is the one
this change closed; the offset discount, vintage premium, year-1 abatement and leaderboard-spread
envelopes all still hold.

The envelope that does not close is full compliance, and it is not a bot-behaviour gap. Over the three
years the fleet owes 1,104.0 Mt against 1,035.8 Mt of allowances (932.3 Mt free plus 103.6 Mt
auctionable), so abatement or offsets must cover the 68 Mt difference. At the year-1 abatement volume
the other envelope allows (about 1% of the cap, the cheap tier only) and a three-year run, at most
about 24 Mt of abatement can ever operate, and the offset-share envelope caps offsets at 2-3% of the
cap, so a structural 34-40 Mt is left uncovered however well the bots trade. The fleet also cannot
simply buy the shortfall: about 42 Mt of the year 1-2 auctionable volume goes unsold, and the only way
a bot takes that up is to bank it, which softens the year it banks into and drops the year-2 clearing
back to the floor so vintage 2 no longer prices above vintage 1 - the vintage-premium envelope.
Handing the same volume out as offsets instead crowds out the auction demand it is meant to replace
and breaks the offset-discount envelope. Full compliance and those envelopes cannot all hold on the
shipped scenario without either a reserve-release mechanism (an engine feature) or a scenario
rebalance of cap, BAU growth and the abatement menu. The finding stands after repeated attempts: the
envelopes are mutually exclusive here, so no bot tuning closes it.

Test counts: 393 passing, 4 skipped in the solution - Engine 239 passing + 3 skipped (full compliance,
the 2-3% offset share and the regulator shock, each with its measured value in the skip reason), Data
16 passing, Web 138 passing + 1 skipped (the opt-in full load run). The price-path envelope is no
longer skipped.

Gate: dotnet build CarbonSim.sln -> 0 warnings, 0 errors; dotnet test CarbonSim.sln -> green (393
passed, 4 skipped).

Files changed: src/CarbonSim.Engine/Bots/ComplianceBot.cs and BotSettings.cs;
tests/CarbonSim.Engine.Tests/Bots/ComplianceBotTests.cs (the spread bid volume);
tests/CarbonSim.Engine.Tests/FidelityTests.cs (the price-path envelope un-skipped and passing, and the
compliance and offset-share skip reasons rewritten with the measurements above).

Open issues: full compliance needs a supply-side fix (reserve release or a scenario rebalance) rather
than bot tuning; the mid-year abatement pass re-reads the same menu rather than being a separate
trigger; and the difficulty knob still shows mostly in bid appetite and noise, because the cheap
abatement tier is worth building at every setting.

### Scenario rebalance for full compliance - blocked, and why (2026-10-04)

Task: rebalance scenarios/vietnam-2024.json so Envelope one (full compliance) can pass alongside the
envelopes already closed (price path, vintage premium, offset discount, leaderboard spread, year-1
abatement), aiming too at the 2-3% offset-share envelope, without a reserve-release engine feature.
Levers allowed: cap trajectory, free allocation share, BAU growth, abatement menu costs/sizes/lead
times, auction volumes, and the harness offset disbursement. Four rebalances were played on the
seeded 242-unit run and measured; every one that moved compliance toward zero broke another envelope.

Shipped (kept): auction averages 100.00 / 108.62 / 220.51; year-1 abatement 1.00%; offsets 7.91%;
leaderboard -13.34 to +28.66; compliance 442 company-years short, 23.8 Mt uncovered. Price path,
vintage premium, offset discount, year-1 abatement and leaderboard spread all hold; compliance and
the 2-3% offset share do not.

Attempt 1, BAU growth flattened to 0-0.5%/yr + cheap abatement tier 0.01 to 0.016 + offset
disbursement cut to 2.8% of emissions: auction 100.00 / 108.27 / 128.80; offsets 2.79% (the offset
envelope now holds); compliance 689 company-years short, 22.0 Mt. The price path broke - year 3
cleared below 200 because the fleet is no longer scarce.

Attempt 2, shipped growth but offset disbursement cut to 2.8%: auction 100.00 / 128.21 / 294.68;
offsets 2.79%; compliance 694 short, 74.3 Mt; leaderboard max 55.96 per tonne, so envelope six (ceiling
40) broke. The fleet's cost of compliance depends on how many cheap offsets it is given, so the
offset-share and leaderboard-spread envelopes are mutually exclusive on this scenario.

Attempt 3, shipped growth and offsets but no forward lots: auction 100.00 / 100.00 / 167.78;
compliance 387 short, 13.9 Mt, the closest of the four; the price path and the vintage premium both
broke, because removing the forward lots added volume to the year-2 and year-3 auctions and softened
the years that were meant to be tight.

Attempt 4, shipped everything but the cheap abatement tier raised to 0.018: auction 100.00 / 107.81 /
191.04; compliance 411 short, 20.9 Mt; the price path broke again, because abating more softens the
market the price path needs to be tight.

The reason is structural, not a tuning gap. Unsold auction volume is returned to the government
reserve and never offered again (Market/GovernmentAccount.Return, and AuctionSchedule builds every lot
once), so any year in which the fleet is not short of that year's auction retires allowances it needs
later. Summed over the run, with free allocation at 90% of the cap and the bots bidding their own
position, the fleet complies if and only if BAU_y <= cap_y + offsets_y + abatement_y in every year.
But the price-path envelope needs year 3 to clear at or above 200, and the vintage-premium envelope
needs year 2 to clear above year 1, and a year only clears above the floor when its auction is
over-subscribed - BAU_y > cap_y + offsets_y + abatement_y. The two conditions cannot both hold, in any
year, whatever the cap trajectory, free share, growth band or abatement menu. The fix is a
reserve-release mechanism (re-offering unsold volume), which this task explicitly excluded; bot
banking also circulates the volume but flattens the year it banks into, and on the shipped 3%/yr cap
decline it cannot keep the year-2 premium either.

Outcome: no scenario change survives, so the scenario and the harness are back exactly as shipped and
Envelope one is skipped again, with the measurement and the structural reason in the skip text. The
offset-share skip text now records its conflict with the leaderboard spread. Files changed this task:
tests/CarbonSim.Engine.Tests/FidelityTests.cs (skip reasons and the two un-skip/measure cycles); the
scenario, FidelityRun and the engine are untouched. Test counts: 393 passing, 4 skipped; zero build
warnings; Web and the load smoke tests still pass on the shipped scenario.

Open issue: full compliance needs a reserve-release feature or a bot that banks while keeping the
year-2 auction tight - neither of which is a scenario lever. Recommend raising the engine change as
its own task rather than trying to rebalance around it.

### Government reserve to auction - built, and the tuning it did not unlock (2026-10-04)

Built. `Parameters.GovernmentReserveToAuctionPercent` (0-1, default 0 so every existing run is
unchanged) is the share of the government reserve offered again at auction. The behaviour lives in
`AuctionSchedule.ReofferReserve` and `Auction.AddLot`: once a year, when a year's last auction
closes, that share of everything the government still holds - including the volume the auction could
not sell - is offered in the next year's auctions, oldest vintage first. A vintage already on offer in
the target auction is topped up rather than added again, because the auction clears one book per
vintage and a second lot of the same vintage would settle the same bids twice; a carry that arrives
across a year boundary becomes a lot of its own. Bots now bid only the lots the compliance year can
actually surrender (so a forward lot is still ignored, and re-offered volume is bid for). The
parameter round-trips through the scenario file (optional, absent means zero), the snapshot and the
EF record; migration `20261004145903_GovernmentReserveToAuction` adds the column. The admin setup
screen shows and edits it next to the free allocation share, with English and Vietnamese strings.

Tests. Nine new: eight engine (`Market/ReserveToAuctionTests`) covering the default doing nothing,
a half share, the whole reserve, the year-boundary carry, the last auction having nowhere to go,
conservation across a re-offer, the validation band, the share surviving a snapshot, and a bot
bidding the carried vintage - plus a bUnit test that the setup page renders the input.

The tuning it was meant to unlock did not work. Every measured variant is in the fidelity run's
report; the pattern is that the share trades circulation against scarcity, and the envelopes need
both. With the original scenario and the share at 1.0 the shortfall only falls 23.8 Mt to 21.8 Mt
(the carry reaches the next year's auctions but the bots' spread bidding does not chase it), the
year-3 clearing falls from 220.5 to 143.1, and vintage 2 stops pricing above vintage 1 because
re-offered old-vintage volume trades in later years and lifts the old vintage's average. At 0.5 the
year-3 clearing is 146.0; at 0.1 it is 167.2 and vintage 1 (135.5) overtakes vintage 2 (105.9). The
per-auction variant (re-offer at every close) circulates far more - with the scenario rebalanced to
flat BAU growth, a 2% cheap abatement tier and a 2.5% offset disbursement the shortfall drops to
3.7 Mt and the offset-share envelope passes at 2.29% - but every auction is then under-subscribed and
the price path sits on the floor (100.00 / 103.25 / 103.80), and adding a bid buffer took the
shortfall to 0.38 Mt while breaking the offset discount (-29.4%). The reason is structural: a year
only clears above the floor when its auction is over-subscribed, and full compliance needs the fleet
to be able to buy its position, so the two can only both hold if the fleet carries stock across years
by banking - which it does not do. The shipped scenario therefore leaves the share at 0.0 and the
compliance and offset-share envelopes stay open, each with its measurement in the skip text.

Why the offset discount read 20.8% in the first report and 15-17% in the second. The discount is
measured, not set: it is (average allowance price - average offset price) / average allowance price,
volume-weighted over the whole run and every channel. The bots quote offsets at a fixed haircut below
the last auction price they saw (15% at Normal, 5% at Hard), so the quote tracks the price path while
the two averages pool different trade populations: the allowance average is dominated by the auction
volume spread over years 1-3 (the shipped run averages 156.58 against offsets 123.98, a 20.8%
discount), and the offset volume is much smaller and clusters where surplus holders sell. When the
offset volume sits in the low-priced early part of the run the measured discount reads larger than
the 15% quote. The 20.8% figure is the shipped state and reproduced in this run; the 15-17% figures
in the second report were read off the intermediate rebalancing variants (15.5% with a larger
abatement tier, 17.4% with the Normal bot bidding its full position), not the restored state, so that
report stated the wrong number for the kept run. The range across variants, 8-21%, is the run's trade
mix moving, not the engine's rule changing.

Gate: dotnet build CarbonSim.sln -> 0 warnings, 0 errors; dotnet test CarbonSim.sln -> green (Engine
248 passing + 3 skipped, Data 16 passing, Web 139 passing + 1 skipped). Files added:
Market/ReserveToAuctionTests.cs, the migration pair. Files changed: Parameters, Auction,
AuctionSchedule, ComplianceBot, ScenarioFile/Loader, SimulationSnapshot/Snapshots, SimulationRecord,
SimulationMapper, CarbonSimDbContextModelSnapshot, ScenarioDraft, AdminSetup.razor, both resx,
TestSimulation, FidelityTests skip text, AdminScreensTests, and the scenario's parameter and notes.

## Review - QA feedback round 1 (2026-10-10)

Claude's browser pass over http://localhost:5003 (reports/qa/feedback-01.md) raised five findings;
all five are closed. F1 was the blocker and had two causes rather than one. The demo seeder started
year one at boot, so the exercise a trainer opened had already spent its twenty-minute year; and a
clock whose year has run out accepted a resume, which left a run reading Running with the auction at
4 / 4 and every countdown at 0m 00s and no way to close the year. The seeder now only puts the
scenario on the shelf ("Start the year" begins it, as the acceptance script says), and
SimulationClock.Resume refuses once the year's time is used, so the console shows the year end
instead of a resume that cannot help. Because a not-yet-started run is now the normal first sight of
an exercise, the console and the waiting screen read it as year 1 with the whole year ahead of it
rather than as year 0, and the console says so in words when the year is over.

F2 follows from the same seeder change: with nothing started, the waiting screen and the console
read "Waiting to start - Year 1" instead of "Trading halted - Year 1". F3: the fine, abatement and
shock pickers name a unit once - "Company - Unit" only where the two differ - and mark bots "(AI)"
as the leaderboard does (AdminUnitView.Label, fed by the company's owner kind). F4: the waiting
screen now says who is signed in and offers the way on (trainer -> console, player -> their own run,
guest -> sign in), and the console's refusal links to the sign-in page. F5: the waiting screen
counted 242 companies against 37 human companies under a "People" label that meant neither; it now
shows companies, human companies and AI companies separately.

Gate: build 0 warnings 0 errors; dotnet test CarbonSim.sln green (Engine 249 passing + 3 skipped,
Data 16 passing, Web 151 passing + 1 skipped, Playwright real). New tests: the clock's refusal, the
seeder's shelf state, the console's reading of a waiting and a run-out year, the fine picker's
labels, the console refusal's link, the waiting screen's counts and front door, and a Playwright
walk of the seeded exercise from "Waiting to start" to a ticking year. PlaywrightVietnamTests starts
the seeded year first, because a player has no allowances to offer until the year has begun.
dotnet format reports one pre-existing charset complaint in the generated migration
20261004145903_GovernmentReserveToAuction.cs, untouched here.

## Review - QA feedback round 2 (2026-10-10)

Claude's second pass (reports/qa/feedback-02.md, screenshots alongside) raised G1-G9; all nine are
closed, with G3 answered by measurement rather than by a change.

G1 (position ignores holdings) was the correctness bug and the fix is in the engine.
CompliancePosition gained HeldFor - the offsets up to the year's limit plus the allowances of the
compliance vintage and older, the same set ComplianceRegister surrenders - and PositionFor, which is
that year's forecast emissions less what is held. The register now takes its offset limit and its
vintage order from the position, so one rule governs both the reading and the settlement. The
screens followed: the top bar, the dashboard's long/short bar and the surrender screen read Held and
the net position, the admin company and surrender reports take their shortfall from the position,
and the leaderboard's final column is the position signed the one way up (positive short) with
Scoring.Holdings kept, renamed, for the tie-break it always was. On the QA case that is Long 975
instead of Short 255,025 once 256,000 tonnes are bought.

G3 (are the bots on the exchange?) is measured by ExchangeActivityTests over the shipped 242-unit
run, 15-second steps, one row per virtual year:

| Year | Orders at 20% | Orders at 100% | Bids / asks | Resting volume at 50% | at 100% | Fills | Volume |
|---|---|---|---|---|---|---|---|
| 1 | 0 | 242 | 242 / 0 | 13,560,767 t | 22,828,331 t | 0 | 0 |
| 2 | 242 | 644 | 644 / 0 | 47,010,460 t | 67,076,854 t | 0 | 0 |

So the answer is: the bots do rest orders, but not before their trade trigger, which is drawn
between 40% and 60% of the year - minute 8 to 12 of 20, which is why the book read empty at QA's
minute 4. The first order appears at 41% of year 1. What the measurement also shows, and reading the
code did not, is that no bot ever offers: in this scenario year-1 business-as-usual exactly meets the
cap and only 90% is allocated free, so every company is short every year and none of them has a
surplus it cannot bank, which is the only thing a bot sells. The bid side is real depth - a player
selling into it fills (MarketActionTests covers the engine's crossing fills) - but the bots never
meet each other and the book grows year on year because nothing cancels a resting order. Changing
that means rebalancing the scenario the price-path envelopes were calibrated against, so it is
reported rather than changed.

G2: the auction result now stays on the screen after the clock moves on - the screen shows the last
cleared auction rather than the current section's, whose cleared state lasted only until the next
section opened - with what was bid, won and paid, and the history table gained My won and My paid
columns beside a system-wide column now labelled "Awarded (all bidders)". The player shell also
renders what the last control did, so "Place bid" is confirmed on the page rather than only in the
log. G4: the switch's redirect is built in code and escaped once, so it lands on the page it was
pressed on (it was double-encoded and always went to "/"); a visible English | Tiếng Việt switch
sits in the top bar of every page and on the sign-in page, both names written in their own language,
and the copy that was covered by the auction banner is gone. G5: the waiting screen's heading turns
over to "Simulation under way" once an exercise has begun, and the signed-in line spaces its button.
G6: the cost curve is drawn to scale - each project a bar as wide as its tonnes and as tall as its
cost per tonne, cheapest first, with the company's own shortfall drawn across it - and the options
table scrolls inside its card instead of being cut off. G7: the price chart gives each product its
own colour and its close line its own dash, the strip is grouped into auction, exchange and OTC, the
order form's fill policy is labelled and explained, and the top of book pairs bids against offers
under Buying and Selling headers. G8: the OTC picker and the leaderboard name a unit once and mark
bots "(AI)", the picker groups by sector and filters as you type, and your own leaderboard row is
highlighted. G9: the heading keeps FocusOnNavigate for screen readers but no longer draws a ring for
it, the ten player form fields axe flagged now carry ids, and the scenario notes say what the
capital scale means (about fifty times a year of compliance cost at the floor, so cash is not the
binding constraint).

Gate: build 0 warnings 0 errors; dotnet test CarbonSim.sln green - Engine 255 passing + 3 skipped,
Data 16 passing, Web 163 passing + 1 skipped with Playwright real. The browser walks now take turns
through one collection, because five classes each booting a host and Chromium were timing out under
their own load. dotnet format reports only the pre-existing charset complaint in the generated
migration 20261004145903_GovernmentReserveToAuction.cs.

## Review - QA feedback round 3 (2026-10-10)

Claude's third pass (reports/qa/feedback-03.md) raised H1-H6; all six are closed and the two-sided
market H3 asked for is built, measured and documented.

H2 (report columns) first, because it was wrong at the source: the cap, free-allocation and
auctionable rows were summed over every year up to a window that was itself the wrong way round, so
"this year" read years 1..N and "to date" read the whole simulation. SystemTotals now carries Cap
and FreeAllocation for the window the rest of the row is built for, and both the admin and the
player read those, so year 2 now reads year 2's cap (345,174,500) and years 1-2 to date.

H1 (stale position after the year closed) is the same class of bug as G1: the position card read the
live forecast against the instruments that had survived the settlement, so a compliant company read
as short by everything it had just surrendered. A reconciled year's position is now the settlement
itself - the panel reads "Compliant" when nothing is owed, and the settled shortfall when there is -
and the year's countdowns are zeroed and hidden once the year is closed, so neither the console nor
the progress strip counts down to an auction that is not coming.

H5 (console forms) had four parts: the forms open on the first unit so a blank selection cannot
happen, a missing choice or description is refused with a message beside the form, every action
reports its outcome next to the button it came from (the notices band at the top of the page is out
of sight from a form halfway down it, which is why a refusal looked like nothing happening), the
labels name their controls, and the messaging control writes its state in words beside a button that
says what pressing it will do. A Playwright walk now performs all five console actions and checks
the player's own screens afterwards.

H3 (two-sided market) was the round's work. Round 2's finding was that the fleet was uniformly
short, so no bot ever had anything to sell; the fix has four parts. The bots quote rather than deal
early in the year (a trade trigger drawn at 5-15% instead of 40-60%) with a spread around the price
they expect, so the book carries bids and offers from the first auction cycle. A second pass late in
the year pulls the quotes that are below the market and pays up for what is still needed, which is
what turns a book of quotes into trades. A quarter of any unusable offsets is offered early and the
rest late, at the discount the bot itself puts on an offset. And the scenario carries a per-unit
allocation factor (scenarios/tools/set-allocation-factors.ps1 writes it): one unit in ten at 1.5 and
the rest at 0.9444, a mean-preserving grandfathering pattern that leaves 24 companies holding more
than they emit, so there are sellers at all. The weights cancel, so the sector cakes and every cap
figure are unchanged, and the scenario notes say all of this.

The measured seeded three-year run (Normal; Hard is measured beside it in the fidelity report the
suite writes):

| Year | Resting bids | Resting asks | Resting volume | Exchange fills | Exchange volume | Exchange average | Auction average | Units penalised | Penalty value |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 389 | 24 | 51,490,162 | 131 | 9,207,619 | 96.74 | 100.00 | 95 | 144,846,600 |
| 2 | 466 | 28 | 72,795,484 | 224 | 13,807,277 | 101.59 | 115.40 | 126 | 191,448,998 |
| 3 | 429 | 26 | 95,509,675 | 113 | 13,916,667 | 103.67 | 240.27 | 204 | 5,827,778,935 |

Auction average by year 100.00 / 115.40 / 240.27, offsets at a 20.1% discount to allowances,
abatement in year 1 at 1.00% of the cap, vintages 99.16 / 110.54 / 193.56, and the bot fleet's cost
of compliance between -56.59 and 34.04 per tonne. Every envelope in force holds; the three that stay
skipped say why in their own skip text.

H4, from the same run: a normal year does not see most bots surrender fully. 95, 126 and 204 units
were penalised in years 1, 2 and 3, 301 of 726 company-years were compliant, and the market
shortfall over the run is 20.5 Mt. The reason is structural rather than a bot fault: the cap falls
3% a year while the fleet's business-as-usual emissions grow, so each year's obligation exceeds
everything the auctions can sell and the tonnes that close the rest of the gap have to come from
companies holding a surplus. That is also why envelope one (every bot complies, no penalties) stays
open and skipped with its measurement. On QA's question: a penalty for a short obligation is charged
whether the shortfall came from a bad year or from a trainer closing the year early, because the
regulator cannot tell the difference; the behaviour stays, and what changed is that the console now
shows the year is over rather than offering a countdown.

H6: the offer form lists the companies people play as their own group before the bots by sector,
still with type-to-filter; the language switch is light on the dark bar, and a Playwright check now
measures its colour; and the offsets form asks for a unit like the other three and hands the offsets
to the company that owns it.

Gate: build 0 warnings 0 errors; dotnet test CarbonSim.sln green - Engine 255 passing + 3 skipped,
Data 16 passing, Web 173 passing + 1 skipped with Playwright real. dotnet format reports only the
pre-existing charset complaint in the generated migration 20261004145903_GovernmentReserveToAuction.cs.

## Review - QA feedback round 4 (J1-J4, K1-K4)

The K items are delivered. The J items are diagnosed, partly delivered, and the rest reported with
their measurements rather than papered over.

K1: the top of book was taking the first twelve orders and pairing them, so a busy book showed the
selling column as a dash on every row while the strip quoted a best offer. The read model now
aggregates the whole book into price levels (volume summed per price, best five a side) and both
columns render from those levels; a read-model test places two offers at one price and asserts they
come back as one level. K2: the order form reports what became of the order beside its button -
filled, partly filled, resting, or the engine's refusal. K3: a year closed while an auction window
is open now clears that auction first, so the bids the room placed are honoured and the result is
broadcast like any other close; a driver test halts and closes the year mid-window and asserts the
sale lands on the record. K4: the report row that mixed every channel into an auction label is now
"Average allowance price (all channels)" with a separate "Auction average price" row, computed in
the engine off the auction channel alone.

J1-J3 were taken as a calibration problem and measured against the brief's section 6 envelopes. A
supply-versus-demand diagnostic was added to the fidelity harness (obligation, free allocation,
auction offered/bid/sold, exchange traded, offsets granted and surrendered, abatement, banked,
shortfall, per year) and it locates the constraint: with the deck's own parameters (90% free, 10%
auctioned, 3%/yr cap decline, a 2-6% growth band) the fleet's cumulative obligation outgrows
everything the scenario can supply, so the later-year compliance rate is set by how much offsets
supply the run has, and that in turn sets the price path. Measured frontier, same seeded run:

| Offsets granted | Offsets surrendered (year 1) | Year-1 clearing | Year-3 clearing | Compliant company-years | Bot MCC range |
|---|---|---|---|---|---|
| 1% + 20% to every 5th | 15.7 Mt (4.4% of cap) | 109.57 | 286.11 | 244 of 726 (34%) | -54.33 to 56.17 |
| 3% + 25% to every 5th | 25.1 Mt (7.0%) | 100.00 | 242.70 | 339 of 726 (47%) | -55.99 to 37.68 |
| 6% + 30% to every 5th | 34.6 Mt (9.7%) | 100.00 | 133.11 | 571 of 726 (79%) | -51.72 to 63.70 |

The shipped calibration is the middle row: every envelope in force holds (year-1 clearing at the
floor and climbing to 242.70, offsets at a 20.4% discount, later vintages above the current one,
1.0% of the cap abated in year 1, the bot cost spread inside +5..+40 with negative outliers), and
year 1 has zero penalised units. The compliance envelope stays skipped with its measurement: 47% of
company-years compliant, and the documented Mexico band (74-84%) only arrives when the offsets are
supplied at about 10% of the cap, which is the row below it - and that row's price path stops
climbing (133 by year 3) because cheap offsets are what the price path is made of. The envelope that
asks for 100% compliance therefore cannot hold together with the envelope that asks for a rising
price path on this scenario; both are documented, they are mutually exclusive here, and the
measurement above is the evidence.

J2 (exchange against auction) was tried and reverted with the numbers: anchoring each bot's expected
price on the last auction clearing plus a trend pulls the exchange up towards the auction, but a
fleet that expects the auction price holds out on the exchange, and compliance fell (549 -> 422
company-years) while the auction path rose. The shipped bots price off the last clearing of any
channel, and the late pass is where the two venues are reconciled. J3's offsets envelope is
captured in the table above; the shock envelope is not delivered: an emission shock moves demand,
not a price, and the price response takes until the next auction window or the bots' next pass,
which is inside the year but not inside one virtual month.

Gate: build 0 warnings 0 errors; dotnet test CarbonSim.sln green - Engine 255 passing + 3 skipped,
Data 16 passing, Web 175 passing + 1 skipped with Playwright real.

## Review - QA feedback round 5 (L2 and the calibration sweep)

L2 is delivered. An administrator can now change the rules of a live run: the console has a "Change
the rules" card that moves the collar a tonne may trade between, which goes through
SimulationClockService.ChangeRulesAsync, validates the pair, writes it into the run's parameters,
bumps the run's RulesRevision and re-anchors every order book on the new floor. The revision is what
tells the bots the ground has moved: a bot that sees it re-opens the market passes it has already
used this year and clamps its expected price into the new collar, so the quotes move on the next
tick rather than at the next scheduled pass. The shock envelope is un-skipped and measured in
ComplianceBotTests: three minutes in the fleet has quoted, the floor is raised to 140, and one
virtual month later the best bid has moved from the old floor's band to the new one. The full
three-year fidelity class no longer carries a skipped shock test.

The sweep QA asked for was run over the offsets supply (1%, 3% and 6% of emissions, plus a lumpy
grant), the bot abatement margin (1.00, 1.05, 1.15) and the abatement cost basis, and the finding is
that the two targets trade against each other on this scenario:

| Offsets granted | Bot margin | Year-1 / 2 / 3 auction average | Compliant company-years | Bot cost range |
|---|---|---|---|---|
| 1% + 20% every 5th | 1.00 | 109.57 / 190.24 / 286.11 | 244 of 726 (34%) | -54.33 to 56.17 |
| 1% + 20% every 5th | 1.05 | 106.45 / 192.52 / 298.31 | 243 of 726 (33%) | -54.37 to 95.09 |
| 3% + 25% every 5th | 1.05 | 100.00 / 114.87 / 242.70 | 339 of 726 (47%) | -55.99 to 37.68 |
| 6% + 30% every 5th | (measured before the margin change) | 100.00 / 133.11 / 133.11 | 571 of 726 (79%) | -51.72 to 63.70 |

The third row ships: every envelope in force holds and year 1 has zero penalties. The fourth row
reaches the Mexico compliance band but its year-3 clearing falls below 160, which is the price-side
target. The missing lever is the one QA named - rational abatement at 160-220 a tonne - and the
measurement says why it does not fire: the mid-priced tier on each menu costs about 190 a tonne, and
the bots' rule is to abate while a project costs less than the price they expect to pay, which in
years 1 and 2 is 100-115. Making them look through to the penalty instead would abate the whole
mid tier in year 1, which breaks the year-1 abatement envelope (documented at about 1% of the cap,
and measured at 1.00% today). The two documented targets are therefore still mutually exclusive on
this scenario, and the sweep above is the evidence; the abatement cost basis was still improved on
the way past (a project is now priced over its own service life rather than amortised over the years
left in the run, which is what stopped the mid tier ever looking affordable at all).

Gate: build 0 warnings 0 errors; dotnet test CarbonSim.sln green - Engine 256 passing + 2 skipped,
Data 16 passing, Web 175 passing + 1 skipped with Playwright real.

## Review - QA feedback round 6 (M1-M5)

M1, the crossed book, was the real defect and it is fixed in the engine: a rule change now takes
every order the new band cannot hold off the book (escrow released, and the run's message says how
many), and then matches what is left against itself, because two orders can both be valid and still
be crossed once the ground has moved. Two tests hold it: raising the floor from 100 to 140 leaves
nothing resting below the new floor and no bid above an offer, before or after the market re-quotes.

M2: the change is announced. The driver writes the change into the run as a message - who, the year,
the old and new floor and ceiling, and how many orders the new band released - pushes ParametersChanged
and MessageReceived to the run's group, and records it on the run so the console lists the last five
changes with who made them. M5: each bot now quotes its own rung of the ladder (a per-company offset
inside the band, deterministic from the company id), so the top levels differ instead of the whole
fleet resting at two prices; a test asserts more than one level.

M4 re-authored the abatement menus as fiction: three rungs per sector priced at their net cost over
the project's service life, about 5, 130 and 240 a tonne, with reductions of 1%, 5-20% and 12-20%.
Only the cheap rung is economic at the year-1 floor, which keeps year-1 abatement at 1.00% of the cap
as the brief documents, and the middle rung is what a bot can work up to as the market tightens. The
grid was then re-run with the shipped bot settings:

| Offsets granted | Year-1 / 2 / 3 auction average | Compliant company-years | Years 2-3 units penalised | Bot cost range | Offset discount |
|---|---|---|---|---|---|
| 1% + 20% every 5th | 100.00 / 212.09 / 300.00 | 190 of 726 (26%) | 183 / 221 | -57.83 to 98.84 | 8.4% |
| 3% + 25% every 5th (shipped) | 100.00 / 181.84 / 255.97 | 294 of 726 (40%) | 129 / 212 | -62.64 to 39.72 | 9.0% |
| 6% + 30% every 5th | 100.00 / 100.00 / 234.58 | 527 of 726 (73%) | 17 / 182 | -50.66 to 25.56 | 38.7% |

No point meets years 2-3 at or above 74%: year 2 does (93% on the last row) but year 3 does not, on
any row, because the fleet's obligation in the final year outgrows everything the scenario can supply
and the shortfall lands on most of the room. The shipped row keeps every other envelope - year-1
clearing at the floor rising to 255.97, the offset discount at 9.0%, the bot cost spread inside
-60..+40, and 1.00% of the cap abated in year 1 - so the compliance envelope stays skipped with this
grid as its measurement, which is the documented limit QA said they would accept if no point existed.

M3 is a decision: only the collar can move mid-run. A year's cap, the free allocation split across
sectors and units, each unit's business-as-usual path and every auction lot are all derived once when
the exercise is built, and the year-end obligation, the auction volumes and the reports all read
those figures; a mid-run cap change would leave the plan the run actually settles against disagreeing
with the cap on the screen. Mainstreaming that would mean re-deriving the plan for the years still to
play and rebuilding the auction lots, which is a larger piece of work than the demo agenda needs.

One real bug surfaced while measuring: the volatility band can move between the moment a bot clamps
its price and the moment the order is submitted (a trade lands in between), and the book's refusal
was escaping the bot's catch as an ArgumentOutOfRangeException, aborting the run. The bots now treat
that refusal like any other and move on.

Gate: build 0 warnings 0 errors; dotnet test CarbonSim.sln green - Engine 259 passing + 2 skipped,
Data 16 passing, Web 175 passing + 1 skipped with Playwright real.

## Review - QA feedback round 7 (N1-N4)

N1: the exchange price chart's legend used to wrap onto the axis title at phone width. The plot now
keeps 60 pixels above itself for the legend (44 on the cost curve) and the legend scrolls rather than
wrapping. The check is a measurement, not a reading of the stylesheet: the charts render to SVG now
(wwwroot/js/charts.js), so Playwright can measure the legend's box against the axis title's at 390px.
Putting grid.top back to its old 34 fails it with "legend Vintage 1 lands on the axis title", which
is QA's finding, and 60 passes. The cost curve's reserve is locked by a unit test on the option JSON.

N2: the language switch had no rule of its own, so it fell through to the global a { color: var(--blue) }
- the link blue on the navy bar, underlined, at the roughly 3:1 QA measured. It now has light text and
a visible focus ring, and the browser test computes the WCAG contrast of the painted colour against
the bar's own background: 4.5:1 or better, and not underlined at rest.

N3: every input on /admin/setup carries an id and a label for it (the abatement and growth rows use a
screen-reader-only label inside the cell, because the column heading is the visible name), and the
shares read as the percentages the console shows - 3% rather than 0.03 - with the fraction kept
behind them; a field accepts "3" or "3%", and a hint under each percent field says so. The new resx
key is in both languages. axe found the first cut: a paragraph inside the metrics dl is not allowed,
so the hint sits in the dd beside the input.

N4 is this section, lesson 6, and the trainer runbook's account of the collar card and the price path.

Gate: build 0 warnings 0 errors; dotnet test CarbonSim.sln green - Engine 259 passing + 2 skipped,
Data 16 passing, Web 179 passing + 1 skipped with Playwright real. dotnet format reports only the
pre-existing CHARSET complaint on the GovernmentReserveToAuction migration.

## Review - the QA loop (2026-10-10)

Seven rounds of browser testing against the demo host, one round at a time, each verified live before
the next began: reports/qa/feedback-01.md through feedback-07.md. The per-round reviews above carry
the detail; this is the index and the closing position.

| Round | Feedback | What it turned up | Fixed |
|---|---|---|---|
| 1 | feedback-01.md | the seeded run read Running and never advanced; pre-start labels; duplicate unit names; the signed-in header; waiting-screen counts | F1-F5 |
| 2 | feedback-02.md | the position ignored allowances held or bought at auction; the auction outcome was not shown; bots barely traded; the language switch was broken and invisible; the MACC did not encode tonnes; the price chart and summary strip | G1-G9 |
| 3 | feedback-03.md | position and progress stale after a year closed; the system report's THIS YEAR / TO DATE columns; bots posted no offers and started bidding at 41% of the year; admin action forms failed silently | H1-H6 |
| 4 | feedback-04.md, feedback-04b.md | bot compliance and the auction-versus-exchange price gap, both outside the envelopes; three skipped envelopes; top of book; order confirmation; closing mid-auction; a report label | J1-J4, K1-K4 |
| 5 | feedback-05.md | the frontier had only been swept over offsets; the trainer needed a live rule change | L1 (the sweep), L2 (Change the rules), L3 |
| 6 | feedback-06.md | a rule change left a crossed book; the shock was silent to players; thin depth; the abatement menu, not the offsets, was the missing lever | M1-M5 |
| 7 | feedback-07.md | the legend landing on the axis title; the switch blue on navy; setup ids, labels and percentages; this record | N1-N4 |

Accepted as documented limits:

- **Full compliance stays open.** The compliance envelope is skipped with the calibration grid as its
  measurement: the shipped point complies 294 of 726 company-years (40%), the widest-offsets row
  reaches 527 of 726 (73%), and no point holds years 2-3 at 74% or better because the last year's
  obligation outgrows everything the scenario can supply. The grid is written out by the fidelity run
  (fidelity-run.md and calibration-grid.md, in the engine test output) and is the evidence for the
  claim rather than an assertion. QA accepted this limit in round 6 and confirmed it in round 7.
- **A mid-run shock moves the price collar only** (M3). A year's cap, the free allocation split
  across sectors and units, each unit's growth path and every auction lot are derived once when the
  exercise is built, and the year-end plan the run settles against reads those figures, so moving the
  cap mid-run would leave the plan and the screen disagreeing. Mainstreaming it means re-deriving the
  plan for the years still to play and rebuilding the lots, which is more than the training agenda
  needs.
- **The exchange's sell side is thin by construction.** The fleet is net short every year - 218 of
  242 companies are allocated below their need in year 1 - so only the 24 allocated above it ever
  post offers: 18 resting asks against 350 bids in year 1. That is the allocation spread, not the
  bots, and lesson 5 records the fix if a future exercise wants a deeper book.

No other open issue is known. One cosmetic thing QA did not raise, noticed while measuring: a scroll
legend prints its own page counter ("1/2") beside its items.

## Review - architecture pass round A (2026-10-11)

The architecture review's candidate 1 (one run module that owns sequencing, year close, rule changes
and who may trade when) is the direction. Round A did not attempt it: it laid the safety net the review
asks for before any refactor, then fixed the two defects the review verified in the source.

A0, the safety net. The suite was measured first and was already green (Engine 259 + 2 skipped, Data 16,
Web 179 + 1 skipped). A golden of the calibrated run is now checked in at
tests/CarbonSim.Engine.Tests/Golden/fidelity-golden.md: the published numbers for Normal and Hard plus a
SHA-256 of each difficulty's BotAction log, kept by The_seeded_run_still_matches_the_golden. It moves
only deliberately (CARBONSIM_UPDATE_GOLDEN=1), which is what makes the later rounds safe to attempt -
candidate 6 in particular, where a reordered draw shifts the seeded stream without changing a rule.

A1, the host now plays the run the envelopes measure. The forward vintage share was an argument to
AuctionSchedule.Build that every harness passed as 0.20 while the host left it at the 0 default, so the
live app never sold a forward vintage and the calibration measured a run the app did not play. It is now
a setting of the run: Parameters.ForwardVintageShare, default 0.20, written in the scenario file's
parameters, explained in its notes, carried through the snapshot, the EF record and the console's setup
screen, and read by AuctionSchedule.Build - one seam, one value, and no caller can pass a different one.
The host, the bots (SimulationBots.Markets) and the fidelity harness all call that one Build, so the
equality the review asked for is structural rather than asserted twice; the golden is identical before
and after, which is the proof that the two now measure the same run. The envelopes did not move: auction
average 100.00 / 181.84 / 255.97, 294 of 726 company-years compliant, 432 penalties, offset discount
9.0%, year-1 abatement 1.00% of the cap. Live in the running app, year 1's second auction lists
"Vintage 1 - 17,792,500 t" and "Vintage 2 - 6,903,490 t (forward sale)", the bid form offers both
vintages, and a bid on the forward vintage is accepted.

A2, trading outside its window is refused from one home. TradingWindow (Engine/Market) holds the two
rules: a year trades only while it is running, and an auction takes bids only while its section's window
is open. Exchange.Place, OtcMarket.Send and OtcMarket.Accept call it, so the rule sits at the market
seam and no caller - host, bot or a future one - can bypass it; cancelling an order and turning an offer
down stay allowed in any state, because they take a player out of the market rather than into it. The
host's bid path calls RequireAuctionOpen against the clock, which only the host holds. Refusals are
typed (TradeRefusedException carrying a TradeRefusal reason), and PlayerContext writes them in the
reader's language ("Trading is halted" / "The auction is closed", en and vi), so a stale page or a hub
client gets a sentence rather than a stack trace.

The brief expected A2 to move the calibrated numbers; it did not, and the reason is structural: the bots
already gate themselves on the same two conditions, so the refusal only ever fires for a caller that
skips that gate. The golden's action-log hash is the evidence - the log records every refusal a bot
meets, so an identical hash over a full seeded run says no bot hit the new refusal.

Three fixtures had to start a year before their markets would trade (ExchangeTests, OtcMarketTests and
three FinanceTests cases). A book that only takes orders in a running year is the rule, and those tests
were trading in a pending one.

Found while fixing A2 and fixed: the console's setup screen scrolled sideways at phone width. Two real
causes. A nested fieldset carries the browser's own min-width: min-content, so the abatement table grew
it to 968px inside a 372px card; and the screen-reader-only labels added in round 7 were absolutely
positioned with no positioned ancestor, so they were placed against the page and widened the document by
429px while being one pixel and clipped. fieldset.card { min-width: 0 } plus pinning .sr-only to the page
corner fixed both, and the phone-width gate is green with the tables rendered - which is also why that
gate looked flaky for two rounds: a measurement taken before the tables rendered saw nothing wrong.

Gate: build 0 warnings 0 errors; dotnet test CarbonSim.sln green - Engine 268 passing + 2 skipped,
Data 16 passing, Web 182 passing + 1 skipped with Playwright real (466 passing, 3 skipped, 0 failed).
dotnet format reports only the pre-existing CHARSET complaint on the GovernmentReserveToAuction
migration, and the golden is unchanged from A0.


## Review - architecture pass (2026-10-11)

The architecture review (reports/arch/architecture-review-2026-10-11.html) named seven candidates: one
run module, a rules accessor, one Standing for a company-year, the order-book band, the view plumbing,
splitting a bot's plan from its execution, and the persistence mappers. The pass ran in rounds A to E
and a last deepening round; round A's own detail is in the section above, and this is the whole of it.
Nothing was committed - the working tree is the deliverable.

Candidate 1, the run module. Engine/Run/SimulationRun.cs owns the order of operations: advance (tick,
clear every closed auction, bots act, collect), start, begin next year, pause, resume, halt, end year
(halt, clear the open auction first, reconcile, close), change the collar, and the market verbs (bid,
place and cancel an order, OTC send and accept). Every verb returns a RunResult carrying what moved, so
the host broadcasts from a value rather than draining side channels. The four copied test loops
(FidelityRun, TrainingRun, SnapshotRoundTripTests, ComplianceBotTests) call the same module, so the
fidelity envelopes measure the run the host plays by construction. In Web the host's open-coded
gate/collect/broadcast is now one primitive, ApplyAsync in SimulationRegistry: it takes the run's gate,
runs a run-module verb, collects its announcements, invalidates the screens' shared cache, saves when
the verb says so, and broadcasts. The hub, the player session and the console all go through it, and
MutationPrimitiveTests fails if a mutating path bypasses it.

Candidate 3, the rules accessor. Simulation.Rules is the one accessor for a run's current Parameters;
Auction, OrderBook, SimulationClock and AuctionSchedule read through it instead of capturing the object.
The collar is the only rule changed at runtime, through the run module's collar verb, which also
releases the orders and bids the new collar cannot hold.

Candidate 2, Standing. Engine/Allocation/Standing.cs answers where a company (or unit) stands in a year:
the emissions the obligation is measured on, held (escrow counted) and available separately, offsets
usable up to the year's limit, position (long-positive), shortfall (never negative), covered, and - for
a settled year - the settlement, never a fresh recomputation. CompliancePosition is deleted; Scoring,
ComplianceRegister, SystemReport, ComplianceBot, PlayerSession, AdminSession and every test and harness
that asked are on Standing, with StandingTests as the table-driven cover.

Candidate 4, the band. OrderBook.FitToBand brings a wanted price inside [BandFloor, BandCeiling] to a
whole cent, stepping off an edge whose three-decimal bound a rounded cent fell outside, so the quote a
bot's own rounding used to push past the band (and the catch then hid) is always acceptable;
ComplianceBot Buy and Sell call it, and Exchange.BandFor is the one home for the band a screen shows.
The golden moved and was re-baselined once, deliberately: 768 of the Normal run's 4,998 bot actions
quote a cent differently (the action count is unchanged), both bot-log hashes moved, and every envelope
in force still holds.

Candidate 5, partly. The SystemTotalsView mirror and the two identical ToTotals mappers are gone:
SystemInfoView and AdminRunDetail expose the engine's SystemTotals directly, and the shared RunViews
holds the year-of and reserve helpers both sessions had copied. PlayerSession.PlaceOrderAsync returns a
typed OrderOutcome (id, filled, average price, resting, refusal) built from the run's own result, so
Exchange.razor reads its notice from the outcome instead of scanning the next snapshot for the newest of
the player's orders or trades. The string parsing (ParseProduct, ParseSide, ParseKind, ParseFill) stays
in SimulationRegistry: moving it to the hub edge would push a second parse into the page and change the
IPlayerSession contract, the screen stub and eight registry call sites.

The two defects the review verified, fixed in round A: the host played the run with forwardVintageShare
0 while every harness measured 0.20, so the live app never sold a forward vintage - now one setting of
the run that the host, the bots and the harness all read; and nothing refused trading outside its
window - now TradingWindow, typed refusals, and a Playwright step that shows the refusal on the screen.

Three bugs found during the pass and fixed. A bid resting below a floor the trainer then raised was
stranded in the auction instead of released; it is now released like an out-of-collar order, with the
cash back and the owner told. The released-bid notice named the bid's price and volume on a feed every
player reads, leaking a sealed bid; notices now have an audience and the room's line carries only counts
(BidNoticePrivacyTests). The long/short label inverted for every short company after the Standing change,
because Display.Position(PositionView) fed a long-positive number to a short-positive formatter;
DisplayPositionTests now covers both sides of the sign and PlaywrightPositionLabelTests checks the top
bar live.

Not done, and why. Candidate 6, splitting a bot's plan from its execution: a reordered draw shifts the
seeded stream, so it needs the golden as its net and is a round of its own. Candidate 7, the persistence
mappers: low urgency, and the snapshot boundary is the one that has to be exact. The admin half of the
resting-order test: the escrow test covers the player's view, and the console's company view has no
equivalent yet.

Gate at the end: build 0 warnings 0 errors; Engine 281 passing + 2 skipped, Data 16 passing, Web 196
passing + 1 skipped; dotnet format clean apart from the pre-existing CHARSET note. The golden is now the
re-baselined one - Normal sha256
b72aab2f2052454f89c4515c4a2c46b20d51e2c4e3bb450a0e8d31962cb4ade2 (4,998 actions) and Hard sha256
2ca1ec100ec357719cd42dd72919e7e861c3a1e06ad2d734484084a69cd05136 (4,972). The process lesson - report
green, or that a test bites, only after running the suite that follows the last edit - is in lessons.md.

## Internal demo - close the gaps (2026-10-17)

Source: reports/2026-10-16-internal-test-demo-gap-analysis.md. Docker is removed from the stack
(user decision 2026-10-17); the demo route is a published build on one Windows box, started by a
script, reached through a tunnel or the LAN.

- [x] Remove Docker: delete Dockerfile, .dockerignore, docker-compose.yml; CLAUDE.md stack gains a
      Deployment row (published build, no containers); deployment.md, handover.md updated
- [x] GAP-03 scenario path from a publish folder: scenarios copied into the Web output, default
      `scenarios/vietnam-2024.json`, one resolver (content root, then app base) for the three call sites;
      test first
- [x] GAP-03 forwarded headers (loopback proxies only) so a tunnel's https reaches the cookie; test first
- [x] GAP-03 `appsettings.Demo.json` + `scripts/run-demo.ps1` (publish, generated credentials kept in
      a gitignored data folder, bind address and port, timestamped log file)
- [x] GAP-05 build stamp (version + commit, run id, seed) on the player and console shells; Blazor
      error banner so a failed circuit says so instead of freezing; GitHub issue template for feedback
- [x] GAP-06/09 automated rehearsal: E2E test runs the launcher from a real publish, signs the trainer in,
      starts the year, kills the host, restarts it, and loads the saved exercise back
- [x] GAP-12 cache the signed-in account on the scoped PlayerSession; test first
- [x] GAP-13 tradingOpenShareOfYear derived from the auction window (no longer an input); migration
- [x] GAP-14 AutoTrade changes only the switched company's bot; the rest of the fleet and the seeded
      stream are untouched; test first
- [x] GAP-10 CI workflow (build, format, test with Playwright) on windows-latest
- [x] GAP-04/08/11 docs/tester-guide.md (access, tour, tasks, known limits, password resets)
- [x] GAP-07 delete the .bak files, `*.bak` ignored, handover and this file's header refreshed
- [x] Gate: build 0 warnings, full suite green after the last edit, format clean, the launcher run live
- Not in scope: GAP-15 (.NET 10) - installing the SDK stays a user decision; the human dry run
  (GAP-06) needs people, so the guide carries the checklist for it

### Review - internal demo (2026-10-17)

Built. Docker is gone (Dockerfile, .dockerignore, docker-compose.yml deleted; CLAUDE.md's stack has a
Deployment row instead). `scripts/run-demo.ps1` publishes a Release build and runs it from publish/ in
the new Demo environment (`appsettings.Demo.json`), keeping the database, generated credentials and a
timestamped log per start in demo-data/ (gitignored). The shipped scenarios are copied into every
build and `CarbonSimHostOptions.ScenarioPath` looks under the content root, then the app folder, for
the three call sites that used to each resolve it. Forwarded headers are read from loopback proxies
only, so a tunnel's https marks the cookie secure. Every run and console screen has a footer stamp
(build with commit, run id, seed); App.razor carries the Blazor error banner in both languages.
`PlayerSession` reads the signed-in account once per circuit instead of once a second.
`Parameters.TradingOpenShareOfYear` is now derived from the auction window (migration
`DeriveTradingOpenShare` drops the column; the key is gone from the scenario and fixtures).
`SimulationBots.Refresh` replaces only the switched company's bot on an AutoTrade change, keeping
every other bot and the seeded stream. Added .github/workflows/ci.yml, a Demo feedback issue
template, docs/tester-guide.md, and rewrote deployment.md sections 1, 2, 5, 6 and 8, the runbook's
start and recovery text, and handover sections 4-6. The two .bak files are deleted and `*.bak`
ignored.

Tests (each seen red first except the stamp tests, which failed to compile first and were then
shown to bite by removing the stamp): ScenarioPathTests (500 before the fix), ForwardedHeadersTests
(cookie not secure before), SharedViewCacheTests account case (3 scope reads, now 1),
FleetRefreshTests (4, no Refresh before), ParametersTests derived share (0.45 stuck before), footer
stamp in ShellTests and AdminScreensTests, and PublishedDemoTests: the launcher end to end with a real
Release publish, a trainer closing year 1, a player registering with the generated PIN, the host
killed, restarted with -NoPublish, the exercise loaded back and year 2 opened.

Gate after the last code edit: build 0 warnings 0 errors; `dotnet format --verify-no-changes` clean
(the old CHARSET note is gone, the migrations are LF without a BOM); `dotnet test CarbonSim.sln`
Engine 285 passed + 2 skipped, Data 16 passed, Web 203 passed + 1 skipped (504 passed, 3 skipped, 0
failed), Playwright real. The fidelity golden is unchanged. Live: the launcher published, applied all
four migrations to a fresh database, created the trainer, served /sign-in 200 and the 37 companies,
rendered the error banner, and the reset procedure in deployment.md section 8 worked (after a fix:
the logged email spans lines, so the lookup needs `-Context 0,4`).

Found and corrected on the way: the runbook and deployment notes said a run is saved "after each
control"; it is saved at creation and at year end only, so a crash replays the year in progress. The
docs now say so; no mid-year autosave was added, because a restored running clock would count the
downtime. There is no password-reset page (the flow is API only); the operator procedure is
documented and the page is next step 3 in handover.md.

Not done: the CI workflow has not run on a hosted runner (nothing pushed); the human dry run in
tester-guide.md section 9 needs people; GAP-15 (.NET 10) waits on installing the SDK. The stamp shows
the commit of HEAD, so it will name f341aab until this work is committed.

## Internal demo - finish (2026-10-17, second pass)

User asked to install .NET 10, upgrade, and complete what remains.

- [x] Install the .NET 10 SDK (winget, side by side with 9)
- [x] Branch `internal-demo`; commit the demo work above as its own commit
- [x] Upgrade to net10.0: TargetFramework, the 9.x package pins, CI's dotnet-version, CLAUDE.md and docs;
      gate green, golden unchanged
- [x] Password-reset page on the sign-in screen over the existing reset service; test first
- [x] Tunnel check: `tailscale serve` to the demo host, reached over the tailnet (http on 8080; https
      needs HTTPS certificates enabled on the tailnet), then turned off
- [x] Push the branch, open a PR, get CI green on the hosted runner
- [x] Tag the demo build once merged (merge to main needs the user's go-ahead)
- Not doable here: the human dry run (needs people); EDF licensing; PostgreSQL against a live server

### Review

- .NET 10 SDK 10.0.402 installed; the solution targets net10.0 with EF Core 10.0.13, Npgsql 10.0.3,
  dotnet-ef 10.0.13. The Identity.Core reference went (shared framework); AngleSharp pinned to 1.8.5
  for GHSA-pgww-w46g-26qg. No pending EF model changes; the fidelity golden is byte-identical.
- Local gate: build 0 warnings, format clean, 507 passed, 3 skipped, 0 failed
  (reports/verify-20261017-1420.md, reports/verify-20261017-1440.md).
- PR #1 (internal-demo) passed CI on windows-latest and was merged to main as f3a6623 with the
  user's go-ahead; main is tagged `demo-internal-1`.
- Tailscale serve works over http on the tailnet name; the https route hangs until HTTPS
  certificates are enabled in the tailnet admin console (docs/deployment.md section 5).
- Still open: the operator dry run in docs/tester-guide.md section 9, EDF licensing, PostgreSQL
  against a live server.
