# Audit A: rule fidelity against the original (2026-10-21)

Read-only audit of `src/CarbonSim.Engine` (plus the host verbs in `src/CarbonSim.Web/Simulations/SimulationClockService.cs` where a rule lives only there) and `scenarios/vietnam-2024.json`, checked against the public sources. I read every clone claim in the code itself and did not rely on comments. No code was changed.

**Sources and short names**
- Spec: `edf-official/CarbonSim-Specs-2018.pdf` (pp. 1-4)
- FAQ: `edf-official/CarbonSim-FAQ-2017.pdf` (question number, page)
- Gloss: `edf-official/CarbonSim-Glossary-2017.pdf` (term, page)
- Cheat: `edf-official/CarbonSim-CheatSheet-2019.pdf`
- Broch: `edf-official/CarbonSim-Brochure.pdf`
- Deck: `vietnam-ta-materials/8-CarbonSim-Simulation_Joshua_EN.pdf` (slide number)
- Ovw: `youtube/overview_54min_transcript.md` [mm:ss]
- Demo: `youtube/demo_23min_transcript.md` [mm:ss]
- DR: `exercise-reports/2023-11-dominican-republic-exercise-report.md`
- Cat: `sim3-instance/resourcestrings_en.json` (string key). This is evidence that a feature exists. It does not tell us how the feature behaves.

**Legend**
- Verdicts:
  - MATCH: the clone does what the source says.
  - MISMATCH: the source is explicit and the code differs.
  - PARTIAL: part of the rule is implemented, or the source is not explicit enough to call a mismatch.
  - MISSING: the source documents the feature and the clone does not have it.
  - UNDOC: the source does not document the rule, so the clone's choice is its own.
- Confidence (Conf):
  - H: the source states it.
  - M: inferred from a transcript, deck, report or string key.
  - L: guessed.
- Impact on a training session: H / M / L.

## Top 10 gaps, ranked by impact x confidence

1. **Leaderboard ranks companies that are out of compliance alongside compliant ones (MISMATCH, Conf H, Impact H).**
   - Source: FAQ #21 (p9) says short players "will not be eligible to win". Spec p3 says ranking is "on the basis of marginal cost of control and compliance status". DR §7.3 says non-compliers "cannot win".
   - Clone: `Reporting/Scoring.cs:197-204` orders by marginal cost, then holdings, then id. Compliance is never a key, so a slightly short company with a low cost/t can rank first.
2. **Auctions sell forward vintages in only one auction per year, and only next year's (MISMATCH, Conf M-H, Impact H).**
   - Source: Deck s19 lists the auction vintages as "Current + future years". At Demo [07:11], year 1, auction 1 offers Y1 8.6 Mt and Y2 6.7 Mt. At Demo [14:16], a Y3 lot clears in auction 2. Demo [19:20] reports 46.6 Mt sold in year 1, which is more than that year's own auction volume. DR Fig. 26 shows vintages 4-6 sold in many auctions of years 3-5. Cat has `ForwardAuction` and `PleaseEnsureTheAllowancesAreAllocatedToAuctionsForTheseVintages`.
   - Clone: `Market/AuctionSchedule.cs:76-91` and `Domain/Parameters.cs:87-95`. One lot of 20% of next year's volume, in the last auction of the year only. Bots never bid on forward lots (`Bots/ComplianceBot.cs:351`).
3. **Resting orders and offers survive the trading halt and year end, and escrowed instruments are left out of reconciliation (MISMATCH, Conf M, Impact H).** This is known open item #1.
   - Source: Cat `CancellationReason_TradingHalted` ("Cancelled - Trading halted"). The FAQ #14 (p6) banking cap is explicit.
   - Clone: `Run/SimulationRun.cs:198-230` cancels nothing. Reconciliation and banking read only available balances (`Compliance/ComplianceRegister.cs:243,260,282-284`), while the position shown on screen counts escrow (`Allocation/Standing.cs:86-105`). The result: a company can be penalised while its screen reads covered, and surplus parked in a sell order escapes the banking cap.
4. **The auction window comes at the end of each quarter, not the start (MISMATCH, Conf M, Impact H).**
   - Source:
     - At Demo [07:11] the auction is open almost as soon as the clock starts.
     - At Demo [10:12] "auction two will start in 1:29", and the frame dated Mar 7 matches that moment.
     - At Demo [11:15] the clock "ticked over to March 31st April 1", which is when auction 2 opens.
     - Ovw [10:07] describes the auction running 2:15 and then an interim period.
   - Clone: `Clock/SimulationClock.cs:61,108` puts the 2:45 gap first and the 2:15 window at the end of each section. So the year opens with no price discovery, and auction 4 closes at the year-end halt, leaving no time to rebalance on its result.
5. **Abatement implementation time is whole years only, with no part-year reduction (MISMATCH, Conf M, Impact H).**
   - Source: Ovw [25:23] gives build times "three quarters of a year to five years". At Demo [05:06-06:08] a 0.75-year retrofit implemented on Jan 1 cuts the year-1 shortfall by 23,720 t, and it starts operating three quarters of the way through the year.
   - Clone: `Domain/AbatementOption.cs:15,70` and `Domain/AbatementMenu.cs:17` store an integer. `Abatement/ImplementedAbatement.cs:17,70-73` operates from `ImplementedIn + N` and delivers a full year's reduction. Every Vietnam menu entry takes 1-2 years, so nothing a player builds in year 1 lowers year-1 emissions.
6. **Changing the auction collar also moves the exchange (MISMATCH, Conf H, Impact M).**
   - Source: Gloss, Ceiling Price (p3) and Floor Price (p4): the collar "can be set for the Primary Market but not the Secondary Market". In the original the exchange trades below the floor: Demo [11:15] shows 96-98 against a floor of 100, and DR §7.3 says floors ended above exchange prices.
   - Clone: on a collar change, `Market/Exchange/OrderBook.cs:89-104` clamps the band anchor into the collar and cancels every exchange order priced outside it. Bots also clamp their price estimate to the collar (`Bots/ComplianceBot.cs:216,281`).
7. **No simulation-horizon view of abatement economics (MISSING, Conf H, Impact M).**
   - Source: FAQ #3-#4 (pp2-3). The original has a Simulation / Project-lifetime toggle. Costs rise as the years pass. Options that cannot operate before the end lose their $/t figure or disappear from the table.
   - Clone: `Domain/AbatementOption.cs:79-93` gives lifetime figures only, and the menu is never filtered.
   - Related bot bug: `Bots/ComplianceBot.cs:303` uses `>` where it needs `>=`, so bots buy projects in the last year that only start operating after the run ends (open item #2).
8. **Mid-run shocks cover only part of what the original allows (PARTIAL, Conf H, Impact M).**
   - Source: Spec p2 lists changes "in allocations, emission growth rates, offset use, etc." within a period. In the Vietnam runs the trainer cut the cap mid-game (assessment §2, citing the final report).
   - Clone: `SimulationClockService.cs:166-358` supports a collar change, a per-unit emission delta (`Allocation/AllocationPlan.cs:156-179`), fines, offset disbursal and adding an abatement. The cap, free share, offset limit and banking limit are `init`-only (`Domain/Parameters.cs:18-42`).
9. **No offset supply in the scenario (MISSING, Conf M, Impact M).**
   - Source: the Gloss Simulation Configuration (p9) shows "Total Offsets Available" and "Total Offsets by Trading System". Cat has `StartingOffsets`, `OffsetSupplyAdjustments` and `TotalOffsetsAvailable`. Demo [20:22] reports 9.2-9.3 Mt of offsets made available in year 1.
   - Clone: `scenarios/vietnam-2024.json` has no offsets. They exist only when the trainer disburses them by hand (`SimulationClockService.cs:297-306`), so the offset market and its discount lesson are absent unless the trainer acts.
10. **Five administrator switches from the spec are missing (MISSING, Conf H, Impact M).**
    - Spec pp1-4: exchange access yes/no, OTC access yes/no, automatic or manual surrender, banking "limit and override", leaderboard visible or not. Spec p1 also says components "can be introduced in a phased approach".
    - None of these exists in `Domain/Parameters.cs` or in the host verbs.

## Full match table

### A. Allowances, vintages, banking, borrowing

| # | Rule as the source states it | Clone (file:line) | Verdict | Conf | Impact |
|---|---|---|---|---|---|
| A1 | A vintage-Y allowance can be used in year Y and later (Gloss Vintage p10; Cheat Key terms) | `Allocation/Standing.cs:170-178`; `Compliance/ComplianceRegister.cs:253` | MATCH | H | H |
| A2 | No borrowing: a future vintage cannot meet an earlier obligation (Gloss Vintage p10) | Same: only the year's vintage and older count | MATCH | H | H |
| A3 | Banking is capped at that year's compliance obligation and the excess is forfeited (FAQ #14 p6; Deck s19; Ovw [40:35]) | `ComplianceRegister.cs:292-321`; `Parameters.cs:36` | MATCH | H | H |
| A4 | Banking concerns "surplus allowances (not offsets)" (FAQ #13-14 p6) | Cap counts allowances only: `ComplianceRegister.cs:282-284` | MATCH | H | M |
| A5 | Offsets carry over, "subject to limitations set by the Administrator" (Gloss Offsets p7; Cheat: "can be used in any year") | They carry over with no limit and no admin setting (`ComplianceRegister.cs:243-248` consumes only what is surrendered) | PARTIAL | M | L |
| A6 | Banking override: "automatically bank all unused allowances (ignores limit)" (Spec p3; Cat `AutomaticUnlimitedAllowanceBanking`) | No override; `BankingLimit` is restricted to 0..1 (`Parameters.cs:36,132`) | MISSING | H | L |
| A7 | Forward vintages can be sold only once acquired; future allocation is not in the briefcase (FAQ #19 p9; FAQ limitation 3 p13) | Sell escrows held stock (`Exchange/OrderBook.cs:259-262`, `Otc/OtcMarket.cs:93`); allocation is granted at each year start (`Run/SimulationRun.cs:112,121`) | MATCH | H | M |
| A8 | Minimum lot is one tonne (Gloss Lot Size p6) | Volume only needs to be > 0; fractions accepted (`OrderBook.cs:353`, `Auction.cs:121`, `OtcMarket.cs:88`) | PARTIAL | H | L |
| A9 | The header long/short "ignores escrow": offering 5 offsets leaves it at +5 (FAQ #18 p8) | Escrowed allowances count (`Standing.cs:86-105`), but usable offsets read the available balance only (`Standing.cs:74-79`), so an offset offer moves the position | MISMATCH | H | L |

### B. Allocation, BAU, cap

| # | Rule | Clone | Verdict | Conf | Impact |
|---|---|---|---|---|---|
| B1 | Admin sets the share of the cap allocated free (Spec p2; Deck s19 90%) | `Parameters.cs:24`; `Allocation/AllocationPlan.cs:57` | MATCH | H | H |
| B2 | Free allocation is set per sector (Spec p4, Sectors row; Cat `FreeAllowanceSectorAllocation`) | One share for every sector, split by emission share (`AllocationPlan.cs:57,66`) | PARTIAL | M | M |
| B3 | "The method of allocation cannot be adjusted" (Gloss Allocation p1); DR §8 proposes benchmarking/grandfathering as future modules | Baseline x `allocationFactor` weight (`AllocationPlan.cs:76-78`): an extra lever, mean-preserving per lessons #5 | PARTIAL | M | L |
| B4 | Allocations are fixed for all years before the start (Gloss Allocation p1) | Plan fixed at creation (`AllocationPlan.cs:54-88`); deposited yearly (`AllowanceLedger.cs:72-97`) | MATCH | M | M |
| B5 | "Free Allowance Allocation % This Year" implies the share can vary by year (Gloss p5) | One constant share (`Parameters.cs:24`) | PARTIAL | L | L |
| B6 | Cap falls at an admin-set rate, 0 to >10%/yr (Spec p4) | `AllocationPlan.cs:56` (geometric from the year-1 cap) | MATCH | H | H |
| B7 | Reduction arithmetic: 3 years gives a 9% target at 3%/yr (Ovw [40:35]); 5-6 years gives 15-18% (Deck s19). This implies year 1 is already reduced, or a linear path | Year 1 = `cap`, geometric after that: a 3-year run reduces 5.9% (`AllocationPlan.cs:56`) | PARTIAL | L | M |
| B8 | Auctioned allowances are inside the cap: cap less free allocation (Gloss Allowance Auction p1; Ovw [25:23]) | `AllocationPlan.cs:101`; `Market/GovernmentAccount.cs:65-78` | MATCH | H | H |
| B9 | BAU growth by sector, low and high (Spec p4; Deck s19 2-6%) | One uniform draw per unit, constant across years (`AllocationPlan.cs:51,85`) | MATCH | H | M |
| B10 | Emission variation between forecast and actual (Gloss Simulation Configuration p9 "Emission Variation Range"; Cat `ComplianceObligationVariationsApplied`) | Deterministic. Only the admin's per-unit shock exists (`AllocationPlan.cs:156-179`) | PARTIAL | L | M |
| B11 | Sector "fractional emissions" (Spec p4) | Emission shares normalised (`AllocationPlan.cs:64-66`) | MATCH | M | L |
| B12 | Intensity targets (Cat `CarbonIntensityTarget`, `CarbonIntensityGrowth`; Gloss Carbon Intensity p3) | None; absolute caps only | MISSING | L | L |

### C. Offsets

| # | Rule | Clone | Verdict | Conf | Impact |
|---|---|---|---|---|---|
| C1 | Offset limit is a % of the compliance obligation (Gloss Offset Limit p7; FAQ #24 p10; Deck s19 10%) | `Standing.cs:52-57` | MATCH | H | H |
| C2 | At year end, as many offsets as the limit allows go first, then allowances (current vintage or banked) (FAQ #24 p10) | `ComplianceRegister.cs:242-271` | MATCH | H | H |
| C3 | Offsets have no vintage (Cheat) | One offset product and book (`Exchange/Exchange.cs:60-69`) | MATCH | H | M |
| C4 | Offset supply is configured: starting offsets, total available, supply adjustments (Gloss p9; Cat `StartingOffsets`, `OffsetSupplyAdjustments`; Demo [20:22] 9.2 Mt available) | None in the scenario; admin disbursal only (`SimulationClockService.cs:297-306`) | MISSING | M | M |
| C5 | Offsets trade at a 5-50% discount, set by the market (FAQ #17 p7; Demo [11:15]) | Market price; bots discount 5-25% (`Bots/BotSettings.cs:36-42`; `ComplianceBot.cs:410`) | MATCH | M | M |
| C6 | Offsets from a secondary system (Spec p4; Gloss "Offsets From Other Systems" p7) | Single system (`Domain/Simulation.cs:134` `.Single()`) | MISSING | H | L |

### D. Compliance and penalties

| # | Rule | Clone | Verdict | Conf | Impact |
|---|---|---|---|---|---|
| D1 | Year-end surrender and banking are automatic by default (FAQ #12 p6) | `SimulationRun.cs:226` -> `ComplianceRegister.Reconcile` | MATCH | H | H |
| D2 | Surrender "Automatic or manual" (Spec p3; Gloss Surrender and Banking p9; Cat `AutomaticSurrender`) | Automatic only | MISSING | H | L |
| D3 | Obligation = reported emissions after abatement (Gloss Compliance Obligation p4) | `Standing.cs:30-40` | MATCH | H | H |
| D4 | Cash penalty per missing allowance, $300 in Vietnam (Deck s19; Ovw [42:38]) | `ComplianceRegister.cs:160,165-173` | MATCH | H | H |
| D5 | Plus one allowance per missing tonne, taken from next year (Ovw [42:38]; Gloss Non-Compliance Penalty p7) | `ComplianceRegister.cs:161-163`; `AllowanceLedger.cs:89-90` (no debit in the final year; a debit larger than the allocation is not carried forward, `:92`) | MATCH | H | H |
| D6 | Penalty multiplier, cash and/or allowance (Spec p4; Cat `CarbonPenaltyMultiplier`, `CashPenaltyRate`) | Fixed cash price plus a debit multiplier (`Parameters.cs:39,42`); no price-linked multiplier | PARTIAL | M | L |
| D7 | Admin fines for illegal behaviour (Spec p3; Gloss Penalty p8) | `ComplianceRegister.cs:215-235`; `SimulationClockService.cs:280` | MATCH | H | L |
| D8 | Orders are cancelled when trading halts (Cat `CancellationReason_TradingHalted`); the banking cap is explicit (FAQ #14) | Orders and offers survive halt and year end (`SimulationRun.cs:198-230`); reconcile and bank use available only (`ComplianceRegister.cs:243,260,282-284`) | MISMATCH | M | H |
| D9 | Temporary shutdown: one year, cannot be undone (Gloss p9) | Engine only (`Abatement/AbatementPortfolio.cs:160`; `Standing.cs:34`); nothing in `src/CarbonSim.Web` calls it; normal operating profit (NOP) is not reduced by it | PARTIAL | H | L |

### E. Auctions

| # | Rule | Clone | Verdict | Conf | Impact |
|---|---|---|---|---|---|
| E1 | Sealed-bid, single-round, uniform price (FAQ #10 p5; Gloss p1; Deck s13) | `Market/Auction.cs:242-298` | MATCH | H | H |
| E2 | Ranked by price, then time submitted (FAQ #11 p6) | `Auction.cs:244-247` | MATCH | H | M |
| E3 | Clearing price = price of the last filled bid (FAQ #10 p5) | `Auction.cs:252-270`; the floor when undersubscribed (consistent with DR Fig. 26, where 26-60% fills clear at 175.00) | MATCH | H | H |
| E4 | Multiple bids; a bid = vintage, volume, unit price (FAQ #10; Gloss Auction Bid p2; Ovw [27:24]) | `Auction.cs:95-137` | MATCH | H | M |
| E5 | Bids only while the auction is open and only for products on offer (FAQ #10) | `SimulationRun.cs:295`; `Auction.cs:104-109` | MATCH | H | M |
| E6 | No bid below the floor or above the ceiling (Ovw [41:38]; Gloss Price Collar p8) | `Auction.cs:111-119` | MATCH | H | H |
| E7 | 4 per year by default; number and duration adjustable (FAQ #10; Spec p2) | `Parameters.cs:56,62`; `AuctionSchedule.cs:82-94` | MATCH | H | M |
| E8 | The window opens at the start of each section, followed by an interim (Demo [07:11]-[11:15]; Ovw [10:07]) | Gap first, window last (`Clock/SimulationClock.cs:61,108`) | MISMATCH | M | H |
| E9 | Equal volume per auction within a year (DR Fig. 26) | `AuctionSchedule.cs:82,159-172` | MATCH | M | M |
| E10 | Current and future vintages in each auction (Deck s19; Demo [07:11], [14:16]; DR Fig. 26; Cat `ForwardAuction`) | 20% of next year, last auction only (`AuctionSchedule.cs:76-91`); bots bid current only (`ComplianceBot.cs:351`) | MISMATCH | M | H |
| E11 | Unsold volume is not rolled into later auctions in the same year (DR Fig. 26: constant offer after part-fills) | Returned to reserve (`Auction.cs:278-281`); re-offered only at year end and only if the % > 0 (`AuctionSchedule.cs:117`) | MATCH | M | M |
| E12 | Government reserve, "amount of allowances withheld" (Spec p3; Cat `GovernmentReservePercent`, `GovernmentReserveToAuctionPercent`) | Re-offer % only (`Parameters.cs:84`); nothing is withheld from the schedule | PARTIAL | M | L |
| E13 | Humans and AI both bid (Spec p2) | `Auction.cs:95`; `ComplianceBot.cs:326-358` | MATCH | H | M |
| E14 | Bid funding and limits: not stated | Price x volume escrowed against cash + overdraft (`Auction.cs:132`; `Finance/CashLedger.cs:64-69,109-121`) | UNDOC | - | L |
| E15 | Pause after each auction, auto-resume after N seconds (Spec p3; Cat `PauseAfterAuction`, `AutoResumeAfterAuctionSeconds`) | Pause: `SimulationClock.cs:368-373`; no auto-resume | PARTIAL | H | L |
| E16 | Price containment reserve or ceiling-triggered release: not stated | None | UNDOC | - | L |

### F. Exchange

| # | Rule | Clone | Verdict | Conf | Impact |
|---|---|---|---|---|---|
| F1 | Continuous book per product: each vintage and offsets (Gloss Exchange Market p4; FAQ #15 p7) | `Exchange/Exchange.cs:60-69`; `OrderBook.cs` | MATCH | H | M |
| F2 | Market, limit and stop-loss orders (Cheat; Deck s16) | `Exchange/OrderTypes.cs:13-18`; `OrderBook.cs:272-319,500-530` | MATCH | H | M |
| F3 | Partial fill and immediate-or-cancel are separate choices; without partial fill an order matches only one same-size counterparty (Deck s16; Ovw [35:30]-[36:31]; Cat `PartialFillsAccepted`, `CancellationReason_ImmediateUnfilled`) | One `FillPolicy {AllowPartial, FillOrKill}` (`OrderTypes.cs:24-28`): no resting all-or-none, no partial-then-cancel | PARTIAL | M | L |
| F4 | Orders more than ±10% from the last trade are rejected (FAQ #16 p7; Ovw [42:38]; Deck s19) | `OrderBook.cs:151-153,378-384` | MATCH | H | M |
| F5 | Market orders have their own variation limit (Cat `MarketOrderVariationLimitPercent`) | Same band limits market-order legs (`OrderBook.cs:423-426`) | PARTIAL | L | L |
| F6 | Band anchor before the first trade: not stated | Auction floor (`Exchange.cs:64,78-80`) | UNDOC | - | L |
| F7 | The collar applies to the primary market only (Gloss Ceiling Price p3, Floor Price p4) | Collar change cancels exchange orders outside it and clamps the anchor (`OrderBook.cs:89-104`; `SimulationRun.cs:264`) | MISMATCH | H | M |
| F8 | Fills at the resting price, partial fills supported (Gloss Partial Fill p8; Demo [15:17]) | `OrderBook.cs:441,459-486` | MATCH | M | M |
| F9 | Orders are good till cancelled (Ovw [45:45]; Deck s21) | Limits rest (`OrderBook.cs:304-308`); behaviour at halt: see D8 | MATCH | M | M |
| F10 | Exchange fees: not stated | None | UNDOC | - | L |
| F11 | Exchange market access yes/no (Spec p3) | No switch | MISSING | H | M |

### G. OTC

| # | Rule | Clone | Verdict | Conf | Impact |
|---|---|---|---|---|---|
| G1 | Seller-initiated offer to a named player; buyer accepts or rejects; no OTC bids (FAQ #26 p10, text inverted but context clear; Gloss Send Offer to Sell p9; Demo [16:17]) | `Market/Otc/OtcMarket.cs:72-140` | MATCH | H | M |
| G2 | OTC is between humans; "you can't trade with a bot" (Ovw [22:22]; Gloss OTC p7) | Any unit can be named (`OtcMarket.cs:76`); bots never answer, so the offer hangs | PARTIAL | H | L |
| G3 | One offer can be missed because another partner accepted it (Cat `OTCState_Missed`) | One named buyer per offer (`OtcMarket.cs:72,95`) | PARTIAL | L | L |
| G4 | Seller can cancel (Cat `OTCState_Cancelled`) | `OtcMarket.cs:143-159`; no expiry | MATCH | M | L |
| G5 | No collar on OTC prices (Gloss Ceiling/Floor Price) | Any price > 0 (`OtcMarket.cs:83`) | MATCH | M | L |
| G6 | OTC market access yes/no (Spec p4) | No switch | MISSING | H | L |
| G7 | Messaging between parties; admin can disable it (Gloss Messaging p6; admin "Disable Messaging") | `SimulationClockService.cs:377` | MATCH | H | L |

### H. Abatement

| # | Rule | Clone | Verdict | Conf | Impact |
|---|---|---|---|---|---|
| H1 | Upfront capital, annual reduction, implementation time, lifetime, annual net revenue, ROI, $/t (Gloss pp1-10; Demo [03:06]) | `Domain/AbatementOption.cs:10-93` | MATCH | H | M |
| H2 | Build time 0.75-5 years; reductions start partway through a year (Ovw [25:23]; Demo [05:06]-[06:08]) | Whole years; full-year reduction from `ImplementedIn+N` (`AbatementOption.cs:15,70`; `ImplementedAbatement.cs:17,70-73`) | MISMATCH | M | H |
| H3 | Irreversible: cannot be undone or sold (Ovw [24:22]-[25:23]; Cat `NOTE_OnceAnEmmissionReductionHasBeenImplemented`; DR §7.3) | `AbatementPortfolio.cs:102-106` | MATCH | H | M |
| H4 | Reductions are additive and the MACC ignores prior projects (FAQ #1 pp1-2) | `AbatementPortfolio.cs:135-148` (capped at emissions) | MATCH | H | L |
| H5 | Status: building / operating / in profit (Gloss My Abatement Implementation Status p6) | `ImplementedAbatement.cs:54-67` | MATCH | H | L |
| H6 | Simulation vs lifetime economics; costs rise with time; options past the horizon lose $/t or vanish (FAQ #3-#4 pp2-3) | Lifetime-only figures (`AbatementOption.cs:79-93`); never filtered | MISSING | H | M |
| H7 | Capital spent from the capital budget (Demo [04:06] compares cost with capital, not capital plus overdraft) | `Capital >= UpfrontCost` required (`AbatementPortfolio.cs:108`) | MATCH | M | L |
| H8 | MACC: $/t against tonnes, cheapest on the left (Gloss MACC p6) | `AbatementOption.cs:86` `NetCostPerTonne` | MATCH | H | L |

### I. Finance

| # | Rule | Clone | Verdict | Conf | Impact |
|---|---|---|---|---|---|
| I1 | Generous starting capital (FAQ #23 p10) | Per-company `capital` in the scenario | MATCH | H | L |
| I2 | Borrowing with an interest rate (Spec p4; Demo [01:03]; top bar "Available Overdraft ($ @ 7%)") | `Company.OverdraftLimit`; `CashLedger.cs:64-69`; 7% on a negative year-end balance (`Finance/CompanyFinance.cs:76-80`) | MATCH | H | M |
| I3 | "Lock interest rate at 0%" (Cat `UnlimitedBudget`) | Rate may be 0 (`Parameters.cs:186`) | MATCH | M | L |
| I4 | NOP and NOP with abatement (Gloss N.O.P. vs Abatement p7; My Finance p6) | `CompanyFinance.cs:33-54,82-98` (paid at year end) | MATCH | H | L |
| I5 | Carbon-cost pass-through to product prices or revenue: not stated | None | UNDOC | - | L |
| I6 | Transfer capital or instruments between a company's units (Gloss Company Management p3; Cat `Transfer*`) | Cash and instruments held at company level (`AllowanceLedger.cs:14`; `Company.cs:58`), so no transfer step exists | PARTIAL | M | L |
| I7 | Penalty when cash is short: not stated | Charged past the credit limit (`CashLedger.cs:100-106`) | UNDOC | - | L |

### J. Scoring and reports

| # | Rule | Clone | Verdict | Conf | Impact |
|---|---|---|---|---|---|
| J1 | Marginal cost = money spent on abatement and compliance instruments / compliance obligation (Gloss Marginal Cost of Compliance p6) | Also counts penalties, fines, interest and abatement running result, and nets sales (`Scoring.cs:17-28,38-48`) | PARTIAL | H | M |
| J2 | Abatement capital timing in the score: a 546k project moved the cost from 0 to $0.03/t on about 1.18 Mt (Demo [06:08]), which fits capital spread over the project life | Full upfront cost charged in the year of implementation (`Scoring.cs:22`) | PARTIAL | L | M |
| J3 | Rank by overall marginal cost, then final long/short; short players cannot win (FAQ #21 p9; Spec p3; DR §7.3) | Compliance not used (`Scoring.cs:197-204`) | MISMATCH | H | H |
| J4 | Annual and overall leaderboard (Spec p4) | `Scoring.cs:31-89` | MATCH | H | L |
| J5 | AI units listed and flagged (video insights §1.11) | `Scoring.cs:193` | MATCH | M | L |
| J6 | Leaderboard visible to players or not (Spec p3; Cat `LeaderboardUnlocked`) | No switch | MISSING | H | L |
| J7 | System report: emissions, sold/surrendered, revenue, prices, offsets, abatement, penalties, this year and to date (Spec p3; Demo [19:20]-[21:23]; DR Fig. 29) | `Reporting/SystemReport.cs:40-124` | MATCH | H | L |

### K. Clock and administration

| # | Rule | Clone | Verdict | Conf | Impact |
|---|---|---|---|---|---|
| K1 | Year length minutes to weeks; unlimited years (Spec p3) | `Parameters.cs:27,59` | MATCH | H | L |
| K2 | Admin presses Begin Year; a year ends after trading halts (video insights §1; Cat `UnableToEndYearBeforeTradingHasHalted`) | `SimulationClock.cs:280-307`; `SimulationRun.cs:118-124` | MATCH | H | M |
| K3 | Pause / resume (FAQ #32 p12; PAUSED banner) | `SimulationClock.cs:222-254` | MATCH | H | L |
| K4 | Warning before trading halts (Spec p4; Cat `WarnBeforeShutdownSeconds`) | `SimulationClock.cs:260-277` | MATCH | H | L |
| K5 | Auction alerts 30 s before start and end (Cat `AuctionPreStartAlert`, `AuctionPreEndAlert`) | `Clock/SimulationClockOptions.cs` `AuctionNotice` = 30 s | MATCH | M | L |
| K6 | Shocks within a period: allocation, growth, offset use (Spec p2); mid-game cap cuts in Vietnam (assessment §2) | Collar, unit emission delta, fines, offsets, abatement (`SimulationClockService.cs:166-358`); cap, share and limits are `init`-only | PARTIAL | H | M |
| K8 | Multiple systems in one simulation; linking; companies spanning systems (Spec p3; Broch; Deck s26; admin "Choose System") | `Simulation.cs:134` `TradingSystems.Single()` | MISSING | H | L |
| K9 | AutoTrade: player can switch it on when the admin enables it; AI trade assistance on/off (FAQ #7 p4; Spec p3; Cat `DisableAutoTradeAssistance`) | Admin-only switch (`SimulationClockService.cs:360`; `Bots/SimulationBots.cs:93`) | PARTIAL | H | L |

### L. AI units

| # | Rule | Clone | Verdict | Conf | Impact |
|---|---|---|---|---|---|
| L1 | AI decides abatement, auction and exchange to resolve its position at least cost (FAQ #6 p4; Gloss AI Player p2) | `ComplianceBot.cs:221-243` | MATCH | H | M |
| L2 | Deterministic, no memory between runs (FAQ #8 pp4-5) | Seeded trigger times (`ComplianceBot.cs:61-70`) | MATCH | H | L |
| L3 | AI does not trade OTC (Ovw [22:22], [39:35]) | Exchange and auction only | MATCH | H | L |
| L4 | Under AutoTrade the AI may cancel human orders it deems illogical (FAQ #7 p4) | `ComplianceBot.cs:525-538` pulls all of the company's orders for the product | MATCH | M | L |
| L5 | A rational player does not build what cannot deliver before the end (FAQ #4 pp2-3) | `ComplianceBot.cs:303` uses `>`, so it builds in the last year (open item #2) | MISMATCH | M | M |
| L6 | Offsets priced at a discount; offset mispricing is a known AI artefact (FAQ #17 p7) | Discounted (`ComplianceBot.cs:410,516`); a second offset buy ignores the resting one (`:447-452` vs `:512-517`, open item #3) | PARTIAL | M | L |
| L8 | The exchange can sit below the auction floor (Demo [11:15]; DR §7.3) | Bot price estimate clamped to [floor, ceiling] (`ComplianceBot.cs:281`); bids reach at most about 9% below the floor | PARTIAL | M | M |

(K7 and L7 were merged into F11/G6 and left out as a non-rule. The numbering keeps the gap.)

## Verdict counts (107 rows)

| Verdict | Count |
|---|---|
| MATCH | 61 |
| PARTIAL | 22 |
| MISSING | 10 |
| MISMATCH | 8 |
| UNDOC | 6 |

## What the sources leave unclear

1. **Undersubscribed auctions.** When bids fall short of supply, does the auction clear at the floor or at the lowest accepted bid? DR Fig. 26 is consistent with the floor, but every bid there may have been at the floor.
2. **Section layout.** Deck s9 shows "2:45 / 2:15" without saying which is the auction. The transcripts point to a 2:15 auction first, then a 2:45 interim. The waiting screen's "45% of the year" agrees with 4 x 2:15 / 20 min.
3. **Cap path.** Linear or geometric? Is year 1 already reduced? Does "3% reduction in allocation" apply to the cap or to the free share (Ovw [40:35])?
4. **BAU growth in year 1.** Does growth already apply in year 1? Demo [01:03] shows 150k short of 1.18 Mt (12.7%), above the 10% a 90% free share implies.
5. **Offsets above the limit in the header.** Does the header long/short count offsets beyond the limit? The FAQ #18 example ignores the limit.
6. **Orders and offers at halt.** What exactly happens to resting orders and pending OTC offers at a halt or year end? Only the string `CancellationReason_TradingHalted` exists.
7. **Offset banking limits.** Gloss Offsets p7 says "subject to limitations"; no such setting is described anywhere.
8. **Marginal cost formula.** Do penalties, interest and sales enter it, and when is abatement capital charged (J1-J2)?
9. **Leaderboard tie-break direction.** Is "final long/short" ranked by closeness to zero or by size of the long?
10. **Penalty debit edge cases.** The make-good debit in the final year, and a debit larger than next year's allocation.
11. **Forward-auction defaults.** Per-vintage volume shares. The string catalog only shows that the admin allocates 100% of each vintage across auctions.
12. **Government reserve.** What "withheld" and "to auction" mean, and when the reserve is offered.
13. **Emission variation range.** Is it random or admin-applied, and when is it applied?
14. **Sector settings.** What "maximum quantity of abatements" and "AI distribution" mean.
15. **Timing of interest accrual and operating-profit payments.**
16. **OTC multi-partner offers and expiry.** `OTCState_Missed` implies multi-partner offers; no expiry rule is given.
17. **DR floor rising.** The DR floors appear to rise by year (150 to 175). It is unclear whether that was an admin change or an escalator.
18. **FAQ #26 wording.** The text says "only buy orders can be placed"; every other source says sell offers only, so this was read as a typo.
19. **Non-partial orders.** Whether a non-partial resting order waits for a same-size counterparty (Ovw [36:31]) or is killed.
20. **Market-order variation limit.** How the `MarketOrderVariationLimitPercent` setting relates to the volatility band.
21. **Intensity targets.** Whether they were ever used in a documented exercise.
