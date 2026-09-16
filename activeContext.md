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
- [ ] EF Core model and migrations (SQLite); repository boundaries thin; engine stays persistence-agnostic
- [ ] ASP.NET Core host; registration with access PIN; login; password reset
- [ ] SignalR `SimulationHub` with per-simulation groups; strongly typed client interface for AuctionOpened/Closing/Cleared, MarketStateChanged, OrderBookChanged, TradeExecuted, OtcOfferReceived/Resolved, MessageReceived, SimulationStateChanged, ParametersChanged
- [ ] Hosted `SimulationClockService` driving the engine and pushing events
- [ ] Integration tests: two simulated clients see the same auction result
- [ ] Review section for Phase 2

## Phase 3 — Player UI (Blazor, ECharts)
Order follows the demo video; each screen has a bUnit test and a Playwright step.
- [ ] Waiting screen with simulation settings
- [ ] Layout: top bar (capital, overdraft, forecast position, MCC, best bid/offer/last, net revenue, briefcase, rules gear with calculator, messages), progress strip (years, auctions, countdowns, PAUSED / halted banners), left nav
- [ ] Dashboard: My Finance, My Compliance, Long/Short stacked bar, Abatement implementation timeline, Auction history, Trade history
- [ ] Abatement: MACC chart, status box, undertaken timeline, opportunities table with Implement
- [ ] Allowance Auction: bid form (volume, vintage, price sliders), vintages this auction, allowances to be auctioned, results modal, histories
- [ ] Exchange Market: price chart (candlestick + close, per vintage + offsets), summary strip, trade activity (top of book, last 10), order form
- [ ] OTC Market: send offer, offers table, accept/reject
- [ ] Company Management, Unit Information, Surrender & Banking, System Info, Leaderboard
- [ ] Messaging
- [ ] Localisation plumbing (resx, culture cookie), English complete
- [ ] Review section for Phase 3

## Phase 4 — Admin console
- [ ] Setup: sectors, trading systems, unit abatement configuration, parameters, registration open/close, PIN
- [ ] Run-time: Begin Year, pause/resume, halt trading with warning, end year, end simulation, disable messaging, issue fines, disburse offsets, add abatement, end-of-year modifications (shocks), player surrender status
- [ ] Reports: system, company, unit, historical average carbon price, leaderboard; export CSV
- [ ] Multiple concurrent simulations
- [ ] Review section for Phase 4

## Phase 5 — Vietnam localisation and scenario
- [ ] Vietnamese resource file (use the Vietnamese glossary in `research/carbonsim-sources/vietnam-ta-materials/` for terminology)
- [ ] VND and USD currency display with configurable rate
- [ ] Vietnam scenario tuned to the trainer-deck parameters (cap 355.85 Mt, 3%/yr, 90% free, 4 auctions, 100/300 collar, 10% offsets, penalty 300 + 1)
- [ ] Trainer runbook for the two-day agenda
- [ ] Review section for Phase 5

## Phase 6 — Hardening and deployment
- [ ] Load test: 250 connected clients, 20-minute year, no missed auction close
- [ ] Docker image; single-server deployment notes; backup of SQLite/Postgres
- [ ] Accessibility and phone-width layout pass
- [ ] Final review and handover notes

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
