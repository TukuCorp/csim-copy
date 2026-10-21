# Fidelity match against the original CarbonSim - 2026-10-21

Three read-only audits compared the clone with the public sources: rules
([audit-a-rules.md](fidelity/audit-a-rules.md), 107 rows), screens
([audit-b-screens.md](fidelity/audit-b-screens.md), 35 acceptance-script steps and 41 admin
functions) and numbers ([audit-c-numbers.md](fidelity/audit-c-numbers.md)). The trainer playtest
([playtest-trainer.md](playtest/playtest-trainer.md)) added four screen findings. I re-read the
sources behind every item marked "checked" below (FAQ pp1-13, the catalog keys, the demo transcript
at 07:11-14:16, the glossary collar rows as quoted).

Rules: 61 MATCH, 22 PARTIAL, 10 MISSING, 8 MISMATCH, 6 not in the sources. The core mechanics
match: vintages and no borrowing, the banking cap, offsets surrendered first up to the limit, the
cash penalty plus a next-year allowance, sealed-bid uniform-price auctions ranked by price then time
inside the collar, the 10% volatility band, seller-only OTC, irreversible additive abatement and
deterministic bots.

## Gap list, in the order they are fixed

Confidence: H = the source states it; M = inferred from a transcript, deck, report or string key.

### Batch 1 - rule mismatches, high confidence, small changes

| # | Gap | Source | Clone | Conf |
|---|---|---|---|---|
| 1 | Resting exchange orders and OTC offers survive a trading halt, so at year end escrowed allowances are neither surrendered nor banked (a covered screen, then a penalty; surplus escapes the banking cap) | Catalog `CancellationReason_TradingHalted`, `CancellationReason_PendingMarketReOpen`; FAQ #14, #18 (checked) | `Run/SimulationRun.cs` halts without cancelling; `Compliance/ComplianceRegister.cs` reads available balances | M-H |
| 2 | Offsets offered for sale drop out of the header long/short; the original's header "ignores escrow" for offsets as for allowances | FAQ #18 p8, worked example (checked) | `Allocation/Standing.cs` counts available offsets only | H |
| 3 | Leaderboard lets a short company rank first; short players are not eligible to win | FAQ #21 p9 (checked); Spec p3 "marginal cost of control and compliance status" | `Reporting/Scoring.cs` sorts on cost per tonne, then holdings | H |
| 4 | Bots build projects in the last year that never operate | FAQ #4 pp2-3 (checked) | `Bots/ComplianceBot.cs` `>` where `>=` is meant | H |
| 5 | Bot places a second offset buy without counting the one resting | FAQ #8, #24 (rational bot) | `Bots/ComplianceBot.cs` short path | M |
| 6 | A collar change moves the exchange (cancels orders outside it, clamps the anchor); the collar is a primary-market control only | Glossary, Ceiling Price p3 / Floor Price p4; demo 11:15 exchange at 96-98 under a floor of 100 | `Market/Exchange/OrderBook.cs` collar re-anchor; bots clamp exchange quotes to the collar | H |

### Batch 2 - year structure, medium confidence, large effect on play

| # | Gap | Source | Clone | Conf |
|---|---|---|---|---|
| 7 | The auction window opens at the start of each quarter; the clone puts it at the end, so year 1 opens with no price discovery and auction 4 closes into the year-end halt | Demo 07:11 (open at the start), 10:12 ("auction two will start in 1:29"), 11:15 (Mar 31 / Apr 1) (checked) | `Clock/SimulationClock.cs` gap first, window second | M-H |
| 8 | Forward vintages are sold through the year (auction 1 of year 1 offers Y1 and Y2; auction 2 a Y3 lot); bots bid on them | Demo 07:11, 14:16 (checked); deck slide 19; DR Fig. 26; catalog `ForwardAuction`, `ScheduleOfSalesByVintage` | `Market/AuctionSchedule.cs` one 20% lot of next year in the last auction; bots skip forward lots | M-H |
| 9 | Abatement build times are fractional (0.75 to 5 years) and a project starts cutting emissions part-way through a year | Overview 25:23; demo 05:06-06:08 (0.75-year retrofit cuts year-1 emissions) | Integer build years, full-year reduction from the next whole year | M |

### Batch 3 - screens

| # | Gap | Source | Conf |
|---|---|---|---|
| 10 | System report: "Total volume" holds secondary sales; government auction volume is never shown; abatement is a project count, not tonnes; penalty count and value share one row | Admin frames 20_admin_a_1170/1338; demo 19:20-21:23 | H |
| 11 | Price report "allowance auction volume" counts every channel (playtest) | Playtest MINOR 1 | H |
| 12 | Leaderboard shows the company where the player belongs; the two leaderboards build the label differently (playtest) | Playtest MAJOR, MINOR 2 | H |
| 13 | Company report cost columns cover the current year only without saying so (playtest) | Playtest NOTE | H |
| 14 | The halt warning never reaches players | Catalog `X_SecondsUntilTradingHalts`; runbook claims it | H |
| 15 | No auction-result notice outside the Auction page | Frame 11; catalog `AuctionEndAlert`, `TradeOfferAlert`, `MessageAlert` | H |
| 16 | Exchange chart has one candle per year, so the in-year path cannot be seen | Demo 10:12-11:15, frame 12 | H |
| 17 | Pending bids are summed, not listed, and cannot be cancelled | Demo 08:12-09:12; catalog `CancelBid`, `BidStateCancelled` | H |
| 18 | Top bar has no virtual date | Frame 02, demo 11:15 | H |

### Batch 4 - calibration (moves the golden; one deliberate re-baseline)

From audit C. The two skipped envelopes are not blocked by supply arithmetic: the clone abates
about 15x less than the original over three years and hands out about 3x the documented offsets
to make up for it, and the year-3 "price at least 200" test has no source.

- C1 re-ground the envelopes: zero penalties in year 1 only; at least 74% of company-years compliant;
  offsets 2-3% of the cumulative cap; year 3 above year 1 and under the ceiling.
- C2 offsets grant to about 2.6% of emissions.
- C3 abatement menu: a cheap rung that operates in its build year; a mid rung near 80-90 per tonne.
- C5 bots bid the full remainder in the last auction and round up.

### Not in this pass

Administrator switches the spec lists (exchange/OTC access, manual surrender, banking override,
leaderboard visibility), mid-run cap and free-share shocks, manual surrender and banking, multiple
trading systems, direct messages and transfers between a company's units. Each is a feature, not a
mismatch in what the clone already does; they are listed in the audits for a later pass.

## Results

(filled in as each batch lands)
