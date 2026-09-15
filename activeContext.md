# activeContext — CarbonSim clone build plan

Status: plan written 2026-09-15, awaiting go-ahead to start Phase 0.
Owner decision still open: keep Blazor Server (assumed) or switch to React before Phase 3.

## Definition of done for v1
A trainer can run the Vietnam two-day agenda end to end: configure a trading system with ~240 units (about 40 human, rest bots), run 3–6 virtual years of 20 minutes with 4 auctions each, watch live prices, pause and shock the market, and show the system report and leaderboard, in English and Vietnamese, on one server. Behaviour passes the fidelity envelopes in `research/2026-09-13_bot-logic-and-numeric-fidelity-options.md` §6 and the click-through in `research/2026-09-13_carbonsim-video-insights.md`.

## Phase 0 — Environment and skeleton
- [ ] Install .NET 10 SDK; confirm `dotnet --version` is 10.x
- [ ] Create `CarbonSim.sln` with Engine, Data, Web projects and two test projects; nullable + warnings-as-errors; EditorConfig
- [ ] `.gitignore` for .NET (bin/obj, .vs, *.user, SQLite files)
- [ ] CI-free local gate: `dotnet build` and `dotnet test` green on empty solution
- [ ] Commit "Phase 0: solution skeleton"

## Phase 1 — Engine core (TDD, no UI)
Entities and state
- [ ] Simulation, TradingSystem, Sector, Company, Unit, Player (human/AI) models
- [ ] Parameters record: cap, reduction rate, free allocation %, BAU growth range per sector, banking limit, offset limit, penalty (cash + allowance), price collar, auctions per year, auction duration, open fraction of year, volatility band, year length, number of years
- [ ] Scenario loader from JSON (`scenarios/vietnam-2024.json` seeded with the 39 firm names, sector shares from the 2025-07 impact report, placeholder abatement menus)
Clock and lifecycle
- [ ] Virtual-year clock with states Pending → Running → Paused / PausedAfterAuction / TradingHalted → YearEnded → GameEnded; warn-before-halt
- [ ] Deterministic RNG seeded per simulation
Allocation and emissions
- [ ] Per-unit free allocation for all years computed up front; BAU emissions growth; forecast long/short position
Abatement
- [ ] Abatement option (upfront cost, annual reduction, implementation time, lifetime, annual net revenue/cost, $/t, forecast ROI); implement → Building → Operating → In Profit; additive reductions; capital check; no undo; temporary shutdown
Primary market
- [ ] Sealed-bid uniform-price auction: multiple bids per unit, rank by price then time, clear to offered volume, all pay clearing price, collar enforcement, per-vintage lots, forward vintages, government reserve
Secondary market
- [ ] Order book per product (vintage / offset): market, limit, stop-loss, partial fill, fill-or-kill; price-time priority; volatility band rejection; escrow of offered instruments
- [ ] OTC: seller-initiated offer to a named unit; accept/reject/cancel; escrow
Compliance
- [ ] Year-end reconciliation: offsets first up to limit, then allowances by vintage; banking capped at obligation; forfeiture of excess; penalty cash + next-year allowance debit; admin fines
Finance
- [ ] Capital, overdraft with interest, normal operating profit, N.O.P. with abatement, net revenue
Scoring and reports
- [ ] Marginal cost of compliance (current year and overall); leaderboard ordering; system totals report ("this year" / "to date")
Bots
- [ ] Rule-based bot per `bot-fidelity` brief §3 with difficulty knob and timing jitter; AutoTrade toggle for human units
- [ ] Fidelity test suite: seeded 242-unit, 3-year run must satisfy the §6 envelopes
- [ ] Review section for Phase 1

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
