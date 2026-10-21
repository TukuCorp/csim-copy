# Audit B: screen and workflow fidelity

Date: 2026-10-21. Scope: the player screens and the trainer console of the Blazor clone, compared with the original's observed screens and string catalog. Code was read only; nothing was run, so every finding comes from reading source, not from a live session. Anything that says "not shown on any screen" was checked by searching the source, and a live click-through should confirm it before a fix is planned.

The original's sources are cited as:
- **demo mm:ss**: `research/carbonsim-sources/youtube/demo_23min_transcript.md`
- **ovw**: `overview_54min_transcript.md`
- **f NN**: the frames under `youtube/frames/`
- **ins §**: sections of `research/2026-09-13_carbonsim-video-insights.md`
- **deck s.N**: slide N of the trainer deck `8-CarbonSim-Simulation_Joshua_EN.pdf`
- **cat `Key`**: a key in `sim3-instance/resourcestrings_en.json`

Clone paths are relative to `src/CarbonSim.Web/` unless they say otherwise.

## Top 10 gaps (ranked by impact x confidence)

| # | Gap | Original | Clone | Impact | Confidence |
|---|---|---|---|---|---|
| 1 | **The exchange price chart has one candle per virtual year**, so in year 1 every product is a single candle. The intra-year price path the trainer narrates (offsets below allowances, both rising) cannot be seen. | Trade-by-trade price lines per vintage plus offsets, read live: demo 10:12-11:15, f 12; cat `PriceHistory`, `RecentTradeIndex` | `Player/Charts.cs:189-232` (x-axis is `Year 1..n`, one OHLC per year); `Components/Pages/Exchange.razor:18` | HIGH | High |
| 2 | **No notification reaches the player away from the screen that caused it.** The auction result appears as a card on the Auction page only. Nothing pops up for auction open, closing soon or ended, an incoming OTC offer, or a new message. The SignalR hub events are not consumed, because the pages poll. | An "Auction has ended" modal over whatever screen is open, with a Close button: f 11, demo 09:12. Alerts: cat `AuctionPreStartAlert`, `AuctionStartAlert`, `AuctionPreEndAlert`, `AuctionEndAlert`, `TradeOfferAlert`, `MessageAlert`, `LeaderboardUnlocked` | `Components/Pages/Auction.razor:79-108`; `Components/Layout/MainLayout.razor:82-86` (polling, no hub client); `Simulations/SimulationRegistry.cs:453-468` (hub sends events nobody listens to) | HIGH | High |
| 3 | **The halt warning never reaches the players.** The trainer can set "warn N seconds before halting", but the engine's `TradingHaltWarned` event is dropped when notices are collected. `PlayerView` carries no halt time, so no countdown or banner appears. `docs/trainer-runbook.md:132-133` says "the room gets a warning first". | cat `X_SecondsUntilTradingHalts`, `WarnBeforeShutdownSeconds`, `SecondaryMarketRemaining` | `Components/Admin/AdminRun.razor:214-217` (input exists); `../CarbonSim.Engine/Run/SimulationRun.cs:351-369` (only auction events become notices); `Components/Layout/ProgressStrip.razor:24-42` | MEDIUM | High |
| 4 | **Pending auction bids are not listed one by one, and a bid cannot be cancelled.** The only trace of an open bid is a summed "You bid" volume in the history row. Its price and its state (in progress, partial, below the clearing price) are not shown. | Each pending bid shows vintage, price, volume and "in progress": demo 08:12-09:12, f 10. Cat `CancelBid`, `BidStateCancelled`, `BidStatePartial`, `BidStateBelow`, `BidStateNone`, `InProgress` | `Components/Pages/Auction.razor:110-122` (`row.MyBids.Sum(...)`); `Player/IPlayerSession.cs:24-46` (no cancel-bid verb) | MEDIUM | High |
| 5 | **Several system-report rows are mislabelled or missing, though the engine computes them.** (a) The row labelled "Total volume" holds secondary-market sales; "Allowances sold by government" (`AuctionVolume`) is never shown. (b) The player screen shows abatement as a count of projects, and the console omits the row; the original gives tonnes, and `AbatementTonnes` exists. (c) "Forecast emissions less abatement" is missing. (d) Penalty count and value share one row labelled "Penalty per tonne". These are exactly the figures the trainer reads out at each year end. | f 20_admin_a_1170 and 20_admin_a_1338 (all 13 rows); demo 19:20-21:23. Cat `AllowancesSoldByGovernment`, `AbatementUndertaken`, `DifferenceBetweenForecastEmissionsAndEmissionsReduced`, `NumberOfCompliancePenaltiesApplied`, `ValueOfGovtPenaltiesApplied` | `Components/Pages/SystemInfo.razor:92,97,99`; `Components/Admin/AdminReports.razor:179-194`; `../CarbonSim.Engine/Reporting/SystemReport.cs:145-158` (`AuctionVolume`, `AbatementTonnes` available) | MEDIUM | High |
| 6 | **The dashboard's "My auction history" shows system totals instead of the player's bids**, and "My trade history" has different columns. | Auction history: Year, Auc #, Vintage, You Bid, Bid Vol, Cleared At, You Won, You Paid. Trade history: Vintage, Offsets, Order Type, Buy/Sell, Partial Fill, Price Limit, Target Volume, Cost, Volume Filled (f 02) | `Components/Pages/Dashboard.razor:84-90` (TotalVolume, ClearedAt, TotalAwarded); `:102-110` (Year, Channel, Product, Price, Volume, Buyer, Seller) | MEDIUM | High |
| 7 | **The top bar has no virtual date, year or auction number, and no overdraft rate.** No month or day string exists anywhere in the web project, so the year never visibly passes as a calendar. | "YEAR 1 / JAN 1 / AUCTION 1" and "Available Overdraft ($ @ 7%)": f 02, f 13, f 16. Demo 11:15: "it's ticked over to March 31st, April 1". Cat `_MonthDay_dtfmt_`, `Month_Jan`..`Month_Dec`, `X_in_Year_Y`, `AvailableOverdraft_X_Y` | `Components/Layout/TopBar.razor:5-20` (grep for month/calendar finds nothing) | MEDIUM | High |
| 8 | **The exchange summary strip covers only the current-year allowance book.** There is no offsets row and no OTC last-trade row, so players cannot read the offset discount off the screen. OTC trades also never move "Last trade". | Rows per product (Year 1, offsets), each with Exchange bid/last/offer, OTC last trade and best bid, and Auction low/cleared/high/average: f 13, demo 11:15. Demo 18:19: "here's the last trade that went off, at 100" after an OTC deal | `Components/Pages/Exchange.razor:19-38, 228-231` (`YearBook` picks one book); `Player/PlayerSession.cs:585-596` (last trade comes from the exchange book only) | MEDIUM | High |
| 9 | **Player-driven surrender and banking, and the year-end company report, are absent.** Surrender and Banking is read-only and always automatic. No per-company profit-and-loss view exists for the debrief. | Cat `VolumeToBeSurrendered_tCO2e`, `VolumeToBeBanked_tCO2e`, `SurrenderAmountSet`, `BankAllowances`, `WarningComplianceShortfall_Penalty_X`, `EstimatedPenalty`, `HasDoneSurrenderAction`, `AutomaticSurrender` (an admin option, so automatic is a setting, not the only mode). Company report: `EndOfYearCompanyReport`, `CapitalOnHandStartOfThisYear`, `AllowancesPurchasedAtAuction`, `ComplianceNetProfitLoss`, `NetAbatementCostProfit`, `LoanInterest`, `OtherFines` | `Components/Pages/SurrenderAndBanking.razor:13-63`; `Components/Admin/AdminReports.razor:64-98` (one line per company) | MEDIUM | Medium (the demo itself runs with automatic surrender: ins §1 "Year-end") |
| 10 | **The trainer console is a different shape with thinner run-time tools.** It has no live auction or exchange views, no "Choose system" scope and no message from the trainer to the room (the mail icon). End-of-year modifications are reduced to one unit's emissions plus the collar. "Pause after each auction" exists in the engine but not in setup. Setup cannot add or edit companies and units, or set free allocation per sector. | Left menu: Reports (System, Company, Unit, Historical Avg Carbon Price), Leaderboard, Issue Fines, Allowance Auction, Exchange Market, End-of-Year Modifications, Player Surrender Status, Disburse Offsets, Add Abatement, Server Admin. Buttons: Begin Year, Disable Messaging, Stop Simulation. Header with date, time left, mail and two status pills (ins §1; f 20_admin_*). Cat `LiveAllowanceAuctionUpdates`, `ExchangeMarketLiveUpdates`, `SystemModifications`, `SectorModifications`, `OffsetSupplyAdjustments`, `PauseAfterAuction`, `AutoResumeAfterAuctionSeconds`, `FreeAllowanceSectorAllocation`, `AddCompany`, `AddUnit` | `Components/Admin/AdminNav.razor:3-14`; `Components/Admin/AdminRun.razor:157-399`; `Components/Admin/AdminSetup.razor:28-193`; `../CarbonSim.Engine/Clock/SimulationClockOptions.cs:10` | MEDIUM | Medium-high |

Just below the cut: multiple trading systems, direct player-to-player messages, transfers between a company's units, cancelling an OTC offer, the abatement "Simulation / Project lifetime" toggle, a working calculator behind the gear, and carbon intensity on Unit Information. All are MEDIUM or LOW for a single-system Vietnam session (see section 2).

## 1. Acceptance script walk (demo_23min plus frames 01-19 and 20_admin_*)

| # | Step | Original (citation) | Clone (file:line) | Verdict | Impact |
|---|---|---|---|---|---|
| 1 | Register: language, PIN, player name, email, password, company | ovw ("access pin at the top... player name... email address... password... company name"); deck s.22-23 | `Endpoints/SignInEndpoints.cs:58-97` (language links, email, display name, password, company select, PIN). No terms-and-conditions acceptance (cat `IAcceptThe_X`) | MATCH | LOW |
| 2 | Waiting screen with Simulation Settings (years, minutes per year, auctions, auctions open 45% of the year) and language switch; redirects to the company page once trading opens | f 01; ins §1.1; cat `TheSimulationHasNotStarted` (redirect) | `Components/Pages/Waiting.razor:13-70` has the settings box and language switch. It lists exercise cards with an Open button and never redirects on its own | PARTIAL | LOW |
| 3 | Top bar: company and unit, Year, date, Auction #, Current Capital, Overdraft (@ rate), Forecast Short/Long, Overall MCC, Best Offer, Last Trade, Best Bid, Net Revenue, calculator, gear, briefcase, bell, mail, player menu | f 02; demo 00:00-01:03; ins §1.2 | `Components/Layout/TopBar.razor:5-42`. No date, year or auction number; no overdraft rate; the sector and unit count stand in for the unit name | PARTIAL | MEDIUM |
| 4 | Progress strip (year and auction segments, countdown text), red PAUSED banner, yellow bar while an auction is open | f 02, f 08; demo 02:04, 07:11; ins §1.3 | `Components/Layout/ProgressStrip.razor:5-43` | MATCH | - |
| 5 | Long/short bar: blue free allocation, red shortfall, total = emissions | f 03; demo 01:03 | `Components/Pages/Dashboard.razor:44-53`; `Player/Charts.cs:152-186` (held plus short stacked). No "current position" marker, and no emissions reference once long | MATCH | LOW |
| 6 | Dashboard panels: My Finance (Capital, Forecast Net Profit, Interest %, Abatements, N.O.P.), My Compliance (Cost of Compliance, Comparison), Abatement status timeline | f 02; ins §1.5 | `Components/Pages/Dashboard.razor:13-74`. The Comparison bar is missing; abatement status is a list, not a Building/Operating/In Profit timeline | PARTIAL | LOW |
| 7 | Dashboard My Auction History and My Trade History columns | f 02 | `Components/Pages/Dashboard.razor:76-112` (system totals; trade-centric columns), see top-10 #6 | DIFFERENT | MEDIUM |
| 8 | MACC: bar width = tonnes, height = $/t, hover names the technology | f 04; demo 02:04-03:06 | `Components/Pages/Abatement.razor:14-17`; `Player/Charts.cs:39-120` (adds a shortfall line). Hover text and the axis name are hard-coded English (`Charts.cs:67-69,116`) | MATCH | LOW |
| 9 | Available Emission Reduction Opportunities table (Project, Upfront Cost, Annual Reductions, Implementation Time, Lifetime, Annual Net, ROI %, Cost $/t, Implement); Simulation / Project lifetime toggle | f 05, f 07; demo 03:06-05:06; ins §1.6 | `Components/Pages/Abatement.razor:48-74` (all columns, plus a Unit column). No lifetime toggle (cat `SimuliationTotal`, `ProjectLifetime`); no irreversibility note (cat `NOTE_OnceAnEmmissionReductionHasBeenImplemented`) | PARTIAL | LOW |
| 10 | Implement: instant; shortfall falls; ABATEMENT box counts (Building/Operating/In Profit/Complete); MCC moves | f 06, f 07; demo 05:06-06:08 | `Components/Pages/Abatement.razor:19-27, 60-71, 92-95` | MATCH | - |
| 11 | Abatement Undertaken as a timeline with legend | f 07; demo 06:08 | `Components/Pages/Abatement.razor:29-46` (a table, no timeline) | PARTIAL | LOW |
| 12 | Auction open: "Vintages this auction" and "Allowances to be auctioned this year" boxes | f 08, f 09; demo 07:11 | `Components/Pages/Auction.razor:65-77` (one total, not split by vintage) | MATCH | - |
| 13 | Gear: rules reminder plus a calculator | demo 07:11 ("there's also a calculator here"); f 09 | `Components/Layout/TopBar.razor:28-33`; `Components/Layout/RulesPanel.razor:3-21` (rules list; the "calculator" is one hint sentence at line 20) | PARTIAL | LOW |
| 14 | Place Bid: volume, vintage, unit price slider, capital cost readout, Place Bid | f 08, f 09; demo 07:11-08:12 | `Components/Pages/Auction.razor:23-57` (adds a unit picker; volume is text) | MATCH | - |
| 15 | Two bids pending (year 1 at 123, year 2 at 101), shown "in progress" | f 10; demo 08:12-09:12 | `Components/Pages/Auction.razor:110-122` (summed volume only; no cancel), see top-10 #4 | PARTIAL | MEDIUM |
| 16 | "Auction has ended" modal on any screen: affected units, vintage, bid vol, won, paid, cost; Close | f 11; demo 09:12-10:12 | `Components/Pages/Auction.razor:79-108` (a card on the Auction page only), see top-10 #2 | PARTIAL | MEDIUM |
| 17 | System Auctions table (Year, Auc #, Vintage, Total Volume, Cleared At, Total Awarded with % filled) beside My Auction History | f 11; ins §1.7 | Merged into `Components/Pages/Auction.razor:110-122`; no % filled (cat `TotalAwarded`, `VintageSold`) | PARTIAL | LOW |
| 18 | Exchange chart: per-vintage and offset series over time | f 12; demo 10:12-11:15 | `Player/Charts.cs:189-232` (yearly candles), see top-10 #1 | DIFFERENT | HIGH |
| 19 | Summary strip: Exchange / OTC / Auction blocks, one row per product | f 13; demo 11:15 | `Components/Pages/Exchange.razor:19-38`, see top-10 #8 | PARTIAL | MEDIUM |
| 20 | Trade Activity: product selector, Orders Accepted, Total Volume, Top of Book ladder (selling above buying), Last 10 Trades with time | f 13; demo 12:15 | `Components/Pages/Exchange.razor:42-108` (no orders-accepted count; side-by-side columns; trades stamped with the year only; the selector shows the raw key "Vintage 1", `Player/PlayerSession.cs:593`) | PARTIAL | LOW |
| 21 | Market order for 63,000 fills against the book; shortfall drops; current orders show it | f 14; demo 12:15-13:15 | `Components/Pages/Exchange.razor:110-165, 255-306` (fill notice); `:168-188` | MATCH | - |
| 22 | Switch to offsets and buy 902 t at market | f 15; demo 13:15-14:16 | Offsets are in the product list: `Components/Pages/Exchange.razor:128-135` | MATCH | - |
| 23 | Limit order at 99.28 rests, then fills below the limit | demo 15:17-16:17 | `Components/Pages/Exchange.razor:140-146, 296-305` | MATCH | - |
| 24 | Order flags: separate Partial Fill and Immediate-or-Cancel checkboxes | f 13; deck s.16 | One checkbox (`Components/Pages/Exchange.razor:155-158`): unticked means partial fills allowed, ticked means fill-or-kill. The behaviour matches the deck's definition, but the form has a different shape | PARTIAL | LOW |
| 25 | OTC Send Offer to Sell: unit to trade with, product with available quantity, volume and cost sliders, capital cost | f 16; demo 16:17-18:19 | `Components/Pages/Otc.razor:15-60` (better picker with filter and groups; no available quantity per product) | PARTIAL | LOW |
| 26 | Buyer sees the offer and accepts or rejects | f 17; demo 18:19 | `Components/Pages/Otc.razor:62-82`. The product shows its raw key at `:72, :96` instead of `Display.Product` | MATCH | LOW |
| 27 | The OTC deal shows as the last trade | demo 18:19 | Last trade reads the exchange book only (`Player/PlayerSession.cs:585-596`) | MISSING | LOW |
| 28 | Year end: surrender is automatic | ins §1 "Year-end" | `Components/Pages/SurrenderAndBanking.razor:13`; engine reconciliation | MATCH | - |
| 29 | System report read at debrief (13 rows, This Year / To Date) | f 20_admin_a_1170; demo 19:20-21:23 | `Components/Pages/SystemInfo.razor:47-54, 77-101`; `Components/Admin/AdminReports.razor:16-30, 173-195`, see top-10 #5 | PARTIAL | MEDIUM |
| 30 | Leaderboard: Rank, Company, Unit (Player), Overall MCC, Final L/S, System; "(AI)" label; penalty visible for non-compliers | demo 21:23-22:23; ins §1.11 | `Components/Pages/Leaderboard.razor:13-23` (adds Cost of Compliance; no penalty column; no per-year view, cat `LeaderboardYear_X`) | PARTIAL | LOW |
| 31 | Trainer ends the year after the halt, then presses Begin Year | demo 22:23; cat `EndYear`, `UnableToEndYearBeforeTradingHasHalted` | `Components/Admin/AdminRun.razor:193-201, 209-212` | MATCH | - |
| 32 | Console layout: left menu of functions, header with date, time left, mail and status pills, action buttons | f 20_admin_*; ins §1 | `Components/Admin/AdminNav.razor:3-14`; `Components/Admin/AdminLayout.razor:41-52`; everything sits on one Controls page (`AdminRun.razor:156-399`) | DIFFERENT | MEDIUM |
| 33 | "Choose System" scopes every report | f 20_admin_*; ins §1 | None (one system per exercise) | MISSING | LOW |
| 34 | Players are warned before trading halts | cat `X_SecondsUntilTradingHalts` | None on player screens, see top-10 #3 | MISSING | MEDIUM |
| 35 | Disable / enable messaging | f 20_admin_*; cat `DisableMessaging`, `EnableMessaging` | `Components/Admin/AdminRun.razor:219-227`; `Components/Pages/Messages.razor:15-18` | MATCH | - |

**Counts (35 steps): MATCH 14, PARTIAL 15, DIFFERENT 3, MISSING 3.** By impact: HIGH 1 (#18), MEDIUM 8, LOW 16, no gap 10. Top-10 #2 is rated HIGH although step 16 alone is MEDIUM, because #2 adds up every missing alert, not just the auction modal.

## 2. Catalog features the clone lacks (grouped by area)

Each feature is listed once, with example keys. Infrastructure keys (generic validation, cookies, terms HTML, email bodies) are left out.

### Player shell and notifications
- Virtual calendar date and year/auction label in the header: `_MonthDay_dtfmt_`, `Month_Jan`, `X_in_Year_Y`.
- Pop-up alerts: `AuctionPreStartAlert`, `AuctionEndAlert`, `TradeOfferAlert`, `MessageAlert`, `LeaderboardUnlocked`.
- Halt countdown and secondary-market timers: `X_SecondsUntilTradingHalts`, `SecondaryMarketStartsIn`, `AllowanceAuctionRemaining`.
- A notice when the trainer changes obligations: `ComplianceObligationVariationsApplied`.
- Briefcase or inventory view: `tCO2e_Inventory`, `Inventory`. The clone's briefcase links to the exchange as "My orders" (`TopBar.razor:23`). Low confidence about what the original opens.

### Dashboard
- Compliance comparison bar: `Comparison`.
- Personal auction-history columns: `YouBid`, `YouWon`, `YouPaid`, `BidVolume`.
- Order-centric trade history: `MyOrderHistory`, `PriceLimit_tCO2e`, `PartialFill`.

### Abatement
- Simulation / project-lifetime toggle: `SimuliationTotal`, `Lifetime`.
- Irreversibility note: `NOTE_OnceAnEmmissionReductionHasBeenImplemented`.
- Summary metrics: `AverageAbatementROI`, `AverageWeightedMarginalCostOfAbatement`, `AbatementPotential_X`.
- Taking a unit offline: `CostToOffline_S_tCO2e`. The clone shows a shutdown flag (`UnitInformation.razor:24`) but has no player action for it.

### Allowance auction
- Per-bid state and cancel: `CancelBid`, `BidStatePartial`, `BidStateBelow`, `InProgress`, `ErrorCancellingBid`.
- Bid counts: `BidsPlaced`, `TotalBids`.
- Results across systems: `AffectedUnits`, `OtherSystems`.
- Per-vintage schedule: `ScheduleOfSalesByVintage`, `RegularAuction`, `ForwardAuction`. Partly covered by the forward-sale label.

### Exchange
- Orders-accepted count: `OrdersAccepted`.
- Order cancellation reasons: `CancellationReason_TradingHalted`, `CancellationReason_ImmediateUnfilled`, `CancellationReason_PendingMarketReOpen`.
- Separate allowance and offset order lists: `AllowanceOrders`, `OffsetOrders`.
- Separate Partial-fill and IOC flags: `PartialFillsAccepted`, `ImmediateOrCancel`.
- Trade timestamps: `Time` in Last 10 Trades.
- The candlestick / close-price switch (`CandlestickGraph`, `ClosePriceGraph`) is covered: the clone overlays both. The missing piece is intra-year granularity.

### OTC
- Cancelling one's own offer: `CancelOffer`, `OfferCancelled`, `ErrorCancellingTrade`.
- The "accepted by a different partner" state: `OTCState_Missed`. This implies one offer can go to several buyers.
- Available quantity in the product picker: `AvailableAllowancesOnHand_tCO2e`, `AvailableOffsetsOnHand_tCO2e`.

### Surrender and banking
- Player-entered surrender and bank volumes with validation: `VolumeToBeSurrendered_tCO2e`, `VolumeToBeBanked_tCO2e`, `SurrenderAmountSet`, `BankAmountSet`, `MaximumOffsetsAbleToBeSurrendered`, `InsufficientOffsetsForTheHighlightedRows`, `AllowanceBankingLimitExceeded`.
- Penalty preview: `WarningComplianceShortfall_Penalty_X`, `EstimatedPenalty`, `CompliancePossible`.
- Availability breakdown: `AllowancesAvailableForComplianceThisYear`, `OffsetsAvailableForComplianceThisYear`, `ResultingLongShortPosition`, `CurrentlyBanked_tCO2w`.

### Company management and unit information
- Transfers of capital, allowances and offsets between a company's units: `CompanyResourceManagementOptions`, `ResourceToTransfer`, `TransferFrom`, `TransferTo`, `CompanyTransferAllowancesSuccessful`.
- Year-end company profit and loss: `EndOfYearCompanyReport`, `CapitalOnHandStartOfThisYear`, `ChangeInCapitalOnHand`, `AllowancesPurchasedInSecondaryMarket`, `OperatingNetProfitLoss`, `ComplianceNetProfitLoss`, `LoanInterest`, `OtherFines`.
- Carbon intensity and a target gauge: `CarbonIntensity`, `CarbonIntensityTarget`, `Intensity`. `CLAUDE.md` lists an "intensity gauge" chart that no screen has.
- Unit detail editing: `EditUnitInformation`, `UnitGeneralInformation`.
- Per-unit metrics: `ProfitPerTonne`, `TonnesReducedViaAbatementX`.

### Leaderboard and system info
- Per-year leaderboard: `LeaderboardYear_X`.
- Filters: `FilterBySector`, `FilterBySystem`, `FilterCompanys`.
- Aggregates: `AverageMarginalCostOfCompliance`, `TotalNumberOfPlayers`, `AverageChangeInCapital`.
- A bank entity: `FinancialInstitution`. Deliberately absent is fine.
- Report rows the clone mislabels or omits: `AllowancesSoldByGovernment`, `AbatementUndertaken` (tonnes), `DifferenceBetweenForecastEmissionsAndEmissionsReduced`, `NumberOfCompliancePenaltiesApplied`, `ValueOfGovtPenaltiesApplied`, `AverageOffsetsSalePriceOtherSystems`.

### Messaging
- Addressed and threaded messages rather than one broadcast log: `Message_To`, `Message_From`, `Thread`, `RecipientRequired`. The clone is broadcast only (`Messages.razor:29-40`).
- Trainer-to-room messages: the console's mail icon. `IAdminSession` has no post-message verb.

### Multiple trading systems and linking
- One simulation hosting several systems, with offset use across systems and exchange rates: `ChooseSystem`, `ChooseTradingSystem`, `TradingSystem_Vintage`, `OffsetsFromOtherSystems`, `FromOtherSystems`, `ExchangeRate`, `DragDropSystemsToAddToSimulation`. Deck s.26 lists "Linking" as a what-if scenario. LOW for the Vietnam single-system run.

### Accounts
- Terms acceptance: `IAcceptThe_X`, `TermsAndConditions`.
- Change password once signed in: `ChangePassword`. The clone has reset by code only.
- Admin user search: `SearchUsers`, `UserInformation`. Partly covered by `/api/admin/accounts`.

## 3. Admin functions: original against clone

| # | Function (source) | Clone | Status |
|---|---|---|---|
| 1 | Begin Year (f 20; cat `BeginYear`) | `Components/Admin/AdminRun.razor:174-177, 198-201` | HAVE |
| 2 | Pause / unpause (cat `PauseSimulation`, `UnpauseSimulation`) | `Components/Admin/AdminRun.razor:179-186` | HAVE |
| 3 | Halt trading with a warning (cat `WarnBeforeShutdownSeconds`, `SecondaryMarketTradingHalted`) | `Components/Admin/AdminRun.razor:188-191, 214-217`; the players never see the warning | PARTIAL |
| 4 | End Year (cat `EndYear`) | `Components/Admin/AdminRun.razor:193-196` | HAVE |
| 5 | Stop / End Simulation (f 20; cat `StopSimulation`, `EndSimulation`) | `Components/Admin/AdminRun.razor:203-206` | HAVE |
| 6 | Disable / Enable Messaging (f 20) | `Components/Admin/AdminRun.razor:219-227` | HAVE |
| 7 | Set Simulation Clock (cat `SetSimulationClock`) | none | MISSING |
| 8 | Pause after each auction, auto-resume, cancel auto-resume (cat `PauseAfterAuction`, `AutoResumeAfterAuctionSeconds`, `CancelUnpause`) | Engine option only (`../CarbonSim.Engine/Clock/SimulationClockOptions.cs:10`); not in setup or controls | PARTIAL |
| 9 | Issue Fines (ins §1; cat `IssueFine`, `UnitToReceiveFine`, `FineDescription`) | `Components/Admin/AdminRun.razor:234-252` | HAVE |
| 10 | Disburse Offsets (ins §1) | `Components/Admin/AdminRun.razor:254-272` (to the unit's company) | HAVE |
| 11 | Add Abatement (ins §1; cat `AddNewAbatement`, `EditAbatement`, `NewAbatementSaved`) | `Components/Admin/AdminRun.razor:274-299` implements an existing option on a unit's behalf. The catalog suggests the original adds or edits an option. Medium confidence | DIFFERENT |
| 12 | End-of-Year Modifications (ins §1; cat `SystemModifications`, `SectorModifications`, `SimulationModifications`, `OffsetSupplyAdjustments`, `EmissionVariation`) | One unit's emissions shock (`AdminRun.razor:301-319`) and a collar change (`:321-350`) | PARTIAL |
| 13 | Player Surrender Status (ins §1; cat `SurrenderStatus`, `HasDoneSurrenderAction`) | `Components/Admin/AdminSurrender.razor:10-56` (no "has attempted surrender") | HAVE |
| 14 | Allowance Auction live view (ins §1; cat `LiveAllowanceAuctionUpdates`, `TotalBids`) | none | MISSING |
| 15 | Exchange Market live view (ins §1; cat `ExchangeMarketLiveUpdates`) | none | MISSING |
| 16 | Reports: System | `Components/Admin/AdminReports.razor:16-30` (rows as in top-10 #5) | HAVE |
| 17 | Reports: Company (cat `EndOfYearCompanyReport`) | `Components/Admin/AdminReports.razor:64-98` (summary line, no P&L) | PARTIAL |
| 18 | Reports: Unit | `Components/Admin/AdminReports.razor:100-130` | HAVE |
| 19 | Reports: Historical Average Carbon Price (cat `HistoricalAverageCarbonPrice`) | `Components/Admin/AdminReports.razor:32-62` (by-year table, no chart) | PARTIAL |
| 20 | Leaderboard with year and filters (cat `LeaderboardYear_X`, `FilterBySector`) | `Components/Admin/AdminReports.razor:132-158` (current only, no filters) | PARTIAL |
| 21 | Choose System (f 20) | none | MISSING |
| 22 | Header: date, time left, status pills, mail (f 20) | Metrics card `Components/Admin/AdminRun.razor:157-169`; no date; no mail | PARTIAL |
| 23 | Trainer-to-player messages | none | MISSING |
| 24 | AI trade per unit (cat `AutoTrade`, `DisableAutoTradeAssistance`) | `Components/Admin/AdminRun.razor:352-379` | HAVE |
| 25 | Server Admin (cat `ServerHealth`, `RestartServer`, `CurrentLogs`, `DownloadDatabase`, `ResetDatabase`) | Saved exercises load/delete (`Components/Admin/AdminRuns.razor:320-363`) | PARTIAL |
| 26 | Return to admin / view a player's screens (cat `ReturnToAdmin`) | Dashboard link `Components/Admin/AdminNav.razor:12` | HAVE |
| 27 | General setup: years, year duration and its units, auctions per year, share of the year auctions are open, gap between auctions (cat `NumberOfYears`, `DurationOfYearUnits`, `AuctionOpenDurationPercentage`, `IntervalTimeBetweenAuctions_mins`) | `Components/Admin/AdminSetup.razor:74, 93-95` (minutes only; auction length in minutes, not a share of the year) | PARTIAL |
| 28 | Finance setup: loan rate, credit-limit factor, unlimited budget, starting capital (cat `LoanInterestRate`, `CreditLimitFactor`, `UnlimitedBudget`, `StartingCapital_X`) | Interest only (`AdminSetup.razor:103-109`) | PARTIAL |
| 29 | Compliance toggles (cat `AutomaticSurrender`, `AutomaticAllowanceBanking`, `AutomaticUnlimitedAllowanceBanking`) | none (always automatic) | MISSING |
| 30 | AI setup (cat `AIDifficulty`, `Novice`, `Expert`, `AIDistributionWithinSector`) | Seed only (`AdminSetup.razor:44`) | MISSING |
| 31 | System design: add, duplicate, rename, delete systems (cat `AddNewSystem`, `DuplicateSystem`, `MaintainSystem`) | none | MISSING |
| 32 | System parameters: emissions, reduction target, collar, banking, offset limits, penalty cash and multiplier, reserve, variation limit, intensity target | Most of them (`AdminSetup.razor:43-109`). Cap reduction is an annual rate rather than an end target (cat `TargetEmissionsReductionsAtEndOfSimulation`); no intensity target | PARTIAL |
| 33 | Schedule of sales by vintage (cat `EditScheduleOfSalesByVintage`, `percent_OfVintage`, `percent_OfFutureVintage`) | One forward-vintage share (`AdminSetup.razor:67-73`) | PARTIAL |
| 34 | Sector configuration: templates, create/rename, emission %, free-allowance %, growth band, variation range (cat `SectorTemplateConfiguration`, `FreeAllowanceSectorAllocation`, `EmissionVariationRange`) | Name, share, growth (`AdminSetup.razor:113-188`); no per-sector free %, no variation range | PARTIAL |
| 35 | Abatement configuration per sector and per unit, max quantity %, allow reimplement (cat `UnitAbatementConfiguration`, `MaximumQuantityOfAbatement`, `AllowReimplement`) | Sector options only (`AdminSetup.razor:125-158`) | PARTIAL |
| 36 | Company and unit management (cat `AddCompany`, `AddUnit`, `AssignSector`, `ModifyUnitNames`, `DeleteUnit`) | none (comes from the scenario JSON) | MISSING |
| 37 | Registration: open, access PIN, link (cat `OpenRegistration`, `AccessPin`, `UserRegistrationLink`) | `Components/Admin/AdminSetup.razor:29-35` | HAVE |
| 38 | Currency management (cat `AddCurrency`, `EditCurrency`, `ExchangeRate`) | Host configuration only (`Components/Layout/RulesPanel.razor:13-17`) | PARTIAL |
| 39 | Translations and glossary (cat `ManageTranslations`, `AddLanguage`, `UploadGlossary`) | Compiled `.resx` only | MISSING (acceptable) |
| 40 | Import and export of setup data (cat `UploadImportData`, `DownloadImportData`, `ExportData`) | Report CSV export only (`Components/Admin/AdminReports.razor:18` and following) | PARTIAL |
| 41 | Save simulation (cat `SimulationWasSaved`) | Saved at creation and at each year end; load (`Components/Admin/AdminRuns.razor:320-363`) | HAVE |

**Totals (41 functions): HAVE 14, PARTIAL 16, MISSING 10, DIFFERENT 1.** The clone also has functions the original's menu doesn't show: a mid-run collar change (`AdminRun.razor:321-350`), CSV export, and a random seed.

## 4. Naming mismatches (names only; the wording stays our own)

| Thing | Catalog name (key) | Clone key / shown name | Note |
|---|---|---|---|
| Console title | `SimulationAdmin` | `AdminConsole` "Trainer console" | Different screen name |
| Setup screen | `SetupSimulation` / `SimulationSetup` | `AdminSetup` "New exercise" | |
| Run list | `RunningSimulation` | `AdminRuns` "Exercises" | |
| Year start | `BeginYear` | `AdminBeginYear` / `AdminBeginNextYear` | The catalog key exists but isn't used. The same pattern runs through all the console keys: Admin* keys duplicate catalog keys (`PauseSimulation`, `UnpauseSimulation`, `EndYear`, `StopSimulation`, `EnableMessaging`, `DisableMessaging`, `IssueFine`, `FineAmount_X`, `FineDescription`, `DisburseOffsets`, `AddAbatement`, `EndOfYearModifications`, `OpenRegistration`, `AccessPin`, `SimulationParameters`) |
| End-of-year modifications | `EndOfYearModifications` | `AdminShock` | Name matches in meaning; the key differs |
| Player surrender status (admin) | `SurrenderStatus` "Player Surrender Status" | Player page uses `SurrenderStatus` (`SurrenderAndBanking.razor:29`); the admin page uses `AdminSurrenderStatus` | The catalog key belongs to the admin screen |
| Automatic surrender | `AutomaticSurrender` = an admin option "Allow automatic surrender" | Used as a player-facing sentence (`SurrenderAndBanking.razor:13`) | Key meaning reused for something else |
| Year duration | `DurationOfYear_mins` = a field label; `AYearWillLastForXMinutes` = the sentence | `DurationOfYear_mins` holds the sentence (`Waiting.razor:56`) | Wrong key for the waiting-screen sentence |
| Abatement status "Complete" | `Complete` | `Expired` (value "Complete") | Key name differs |
| Top-bar position | `ForecastShortPosition` / `ForecastLongPosition` | `CurrentPosition` | Deliberate per lessons #3 (same meaning: it counts holdings); only the name differs |
| Overdraft | `AvailableOverdraft_X_Y` (carries the rate) | `AvailableOverdraft` | Rate dropped |
| Overall MCC | `OverallMarginalCostOfCompliance` "Overall..." | Value "Marginal cost of compliance" | "Overall" dropped; a second MCC on the dashboard is called "This year, per tonne" (`CurrentMarginalCostOfCompliance`) |
| Interest | `Interest_X` / `LoanInterestRate` | `InterestRate` | |
| Price history | `PriceHistory` | `PriceChart` (value "Price history") | Catalog key exists, unused |
| Last trades | `LastTenTrades` | `LastTrades` | |
| OTC market | `OTCMarket`, `OverTheCounterTrading` | `OtcMarket` | Case only |
| Unit to trade with | `UnitToTradeWith` | Same key, value "Counterparty unit" | Fine (own wording) |
| Offers table | `Offers` (one table) | `OffersToAnswer`, `MyOffers` (two tables) | Split screen |
| Holdings | `AllowancesOnHand_tCO2e`, `OffsetsOnHand_tCO2e`, `Inventory` | `Holdings`, `Held`, `Available`, `Committed`, `EscrowedCash` | Invented names where catalog names exist |
| Sector table | `TotalSystemEmissionsForSector` | `SectorEmissions` "Sectors" | |
| System report header | `SystemTotals` | `Product` column (`SystemInfo.razor:50`, `AdminReports.razor:21`) | Wrong column name for a row-label column |
| Allowances sold by government | `AllowancesSoldByGovernment` | `TotalVolume` (holding secondary-market sales) | Mislabel, see top-10 #5 |
| Penalties row | `NumberOfCompliancePenaltiesApplied`, `ValueOfGovtPenaltiesApplied` | `PenaltyPerTonne` | Mislabel |
| Abatement row | `AbatementUndertaken` (tonnes) | `Abatement` (count) | Different figure |
| Volatility band | `MarketOrderVariationLimitPercent` | `VolatilityBand` | |
| Price collar | `PriceFloor_S_tCO2e`, `PriceCeiling_S_tCO2e`, `PriceCollar` | `AuctionFloorPrice`, `AuctionCeilingPrice` | |
| Offset limit | `MaximumPercentOfOffsets`, `OffsetLimit` | `OffsetUsageLimit` | |
| Penalty | `PenaltyCashPrice_S_tCO2e`, `CarbonPenaltyMultiplier` | `PenaltyPerTonne`, `PenaltyAllowanceDebit` | |
| Year length / auctions | `DurationOfYear`, `NumberOfAllowanceAuctionsYear`, `AuctionOpenDurationPercentage` | `YearLength`, `AuctionsPerYear`, `AuctionDuration` (minutes) | Different parameterisation as well as name |
| Forward vintage | `FutureVintage`, `percent_OfFutureVintage` | `ForwardVintageShare` | |
| Auto trade | `AutoTrade` | `AutoTrade` value "Automatic trading"; admin `AdminAutomaticTrading` | Duplicate key |
| Briefcase | (no catalog key; `Inventory` likely) | `Briefcase` "My orders", linking to the exchange | Low confidence about the original's target |

Hard-coded English that bypasses the resources (it shows up in Vietnamese too): MACC hover and axis text (`Player/Charts.cs:67-69, 116`), the chart's "Year n" axis (`Charts.cs:197`), the raw product key in OTC tables (`Otc.razor:72, 96`) and in the Trade Activity selector (`PlayerSession.cs:593`), "(AI)" in the admin leaderboard (`AdminReports.razor:150`), and "(min)" and "cost" in console labels (`AdminSetup.razor:94-95`, `AdminRun.razor:290`).
