# CarbonSim video insights

Date: 2026-09-13
Sources: two UCSC Teaching and Learning Center recordings (April 2020) of Josh Margolis running CarbonSim for an environmental-studies class. Auto-captions pulled with yt-dlp, cleaned into minute-stamped transcripts; the demo video was downloaded at 360p and 23 screen frames extracted. All saved under `research/carbonsim-sources/youtube/`.

| Video | Length | What it is |
|---|---|---|
| `VdvWjN1J3jc` "Cap and Trade Sim" | 54:08 | Lecture: ETS basics, CarbonSim basics, rules of the exercise, registration, wrap-up. Same content as the Vietnam trainer deck, spoken. |
| `SJpV_aNCSWY` "Carbon Sim Demo" | 23:11 | The "open hand round": Josh plays one full virtual year on screen, then shows the admin console. This is the only screen-by-screen walkthrough we have. |

## 1. New information not in any document

### The Simulation Admin console (never documented before)
Left menu, top to bottom: Reports (System, Company, Unit, Historical Average Carbon Price), Leaderboard, Issue Fines, Allowance Auction, Exchange Market, End-of-Year Modifications, Player Surrender Status, Disburse Offsets, Add Abatement, Server Admin. Action buttons at the bottom: **Begin Year**, **Disable Messaging**, **Stop Simulation**.
Header: "Simulation Admin", current date ("Dec 31, Year 1"), "Time Left", a mail icon, and two status pills: "Allowance Auction starts in 0 minutes 0 seconds" and "Secondary Market Trading Halted 00:00".
A "Choose System" dropdown scopes every report to one trading system (here "Mexico"), confirming that one simulation can host several systems.
URL pattern seen: `carbonsim.org/admin/leaderboard`.

### The player screens in sequence (frames `01`–`19`)
1. **Waiting screen**: "Waiting For Simulation To Start" with the Simulation Settings box: years in simulation, minutes per year, auctions per year, "Allowance auctions are open for 45% of the year". Language flags top-left.
2. **Persistent top bar**: company name and unit; Year and date; auction number; Current Capital; Available Overdraft ($ @ 7%); Forecast Short/Long Position; Overall Marginal Cost of Compliance; Best Offer, Last Trade, Best Bid, Net Revenue; briefcase, gear (rules + calculator), envelope (messages); player name dropdown.
3. **Progress strip** under the top bar: "Simulation progress" (Year 1, Year 2, Year 3 segments) and "Section progress" (Auction 1..4 segments with a countdown text such as "Auction 1 - Year 1 - 2 minutes 15 seconds"), plus a full-width red **PAUSED** banner when the clock is stopped and a yellow bar while an auction is open.
4. **Left nav**: Dashboard, Company Management, Unit Information, Abatement, Allowance Auction, Exchange Market, OTC Market, System Info, Surrender And Banking (Leaderboard is reachable too).
5. **Dashboard** panels: My Finance (Capital, Forecast Net Profit, Interest %, Abatements count, N.O.P.), My Compliance (Cost of Compliance, Comparison bar), My Long/Short Position (stacked bar: blue free allocation, red shortfall, "Current position" marker), My Abatement Implementation Status (timeline with Building / Operating / In Profit legend), My Auction History table, My Trade History table.
6. **Abatement**: MACC bar chart (width = tonnes, height = $/t, hover shows technology), legend, ABATEMENT status box (Building / Operating / In Profit / Complete counts), Abatement Undertaken timeline, and the Available Emission Reduction Opportunities table with columns Project, Upfront Capital Cost, Annual Emission Reductions, Implementation Time (yrs), Project Lifetime (yrs), Annual Net Revenue/Cost, Forecast ROI %, Cost ($/tCO2e), Implement button. A "Simulation / Project lifetime" toggle sits top-right.
7. **Allowance Auction**: Place Bid panel (Bid Volume slider, Vintage dropdown, Unit Bid Price slider, Capital Cost readout, Place Bid); right-hand boxes "Vintages this auction" (volume per vintage) and "Allowances to be auctioned this year". After close: an "Auction has ended" modal listing each affected unit's bid volume, won, paid, cost; then My Auction History and system Auctions tables (Year, Auc #, Vintage, Total Volume, Cleared At, Total Awarded with % filled).
8. **Exchange Market**: price chart with per-vintage series and offsets; a summary strip (Exchange Market best bid / last trade / best offer, OTC Market, Auction low / cleared / high / average); Trade Activity panel (vintage selector, orders accepted, total volume, Top of Book with selling/buying sides, Last 10 Trades); Exchange Market Orders form (Buy/Sell, Order Type: Market / Limit / Stop Loss, Partial Fill / Immediate-or-Cancel checkboxes, vintage chips Year 1/2/3, Offsets chip, Target Volume slider, Place Order).
9. **OTC Market**: Send Offer To Sell (Unit to trade with, Allowances and Offsets with available quantity, Volume slider, Cost slider, Capital Cost readout, Send Offer to Sell); Offers table (Unit, product, cost, volume, capital, State). Buyer sees the offer and accepts/rejects. Sellers only; buyers must negotiate verbally.
10. **System report** (same table as admin): Forecast emissions, allowances sold/surrendered, auction revenue, average allowance and offset prices, abatement undertaken, emissions reduced, penalties count and value, "This Year" vs "To Date".
11. **Leaderboard**: Rank, Company, Unit Name (Player), Overall Marginal Cost of Compliance ($/tCO2e), Final Long/Short Position, System. AI units are labelled "(AI)". A bank entity sits at rank 1 with a huge negative cost and should be ignored.

### Behaviour details observed live
- Clock: 20 real minutes per virtual year; the date advances visibly ("Jan 1", "Mar 7", "Apr 7", "Aug 10", "Sep 2", "Dec 31").
- Auctions: 2 min 15 s each, four per year, opened for 45% of the year; countdowns of "starts in" and "ends in"; bids are pending while open and resolve with a modal.
- Implementing an abatement is instant on click and immediately reduces the forecast shortfall by the annual reduction; marginal cost updates immediately (0 to $0.03/t in the demo).
- Market orders fill against the book with partial fills; limit order filled below the limit price. Offsets trade at a discount (about 3 to 5% here) because of the 10% use limit, and bots know it.
- Year-end: surrender is automatic; system report then leaderboard are shown; "Begin Year" is pressed by the admin.
- Bot benchmark stated in the lecture: the bots finished the 3-year Mexico scenario with an overall marginal cost between about +$31 and -$40 per tonne; humans are told to beat that.

## 2. Confirmations of things we had only on paper
- Sealed-bid uniform-price auction worked through with the 125,000-tonne example (clears at 45 when demand reaches supply; everyone pays 45).
- Rules of the exercise identical to the Vietnam deck: cap ~356 Mt, 242 enterprises (38 human), 3%/yr allocation cut, BAU growth 2–6%, banking up to 100% of the obligation, 90% free, 10% offset limit, floor/ceiling 100/300, penalty 300 plus one next-year allowance, 10% exchange volatility band.
- "Don't wait to abate", "participate in all markets", "post two-way markets", "orders are good till cancelled", "finger mistakes", "the system is not financial grade".

## 3. What the videos still do not show
- Company Management, Unit Information, Surrender and Banking, System Info screens (named in nav only).
- Admin setup screens (sector and system configuration, unit abatement configuration, registration control) and the End-of-Year Modifications form.
- Any bot decision logic beyond "they comply at lowest cost and price offsets at a discount".
- Video quality is 360p only (YouTube refuses higher formats to non-browser clients), so exact numbers in frames are partly illegible; the transcript supplies most of them.

## 4. Impact on the replication estimate
- Player-screen confidence rises from ~70% to ~85%: every routinely used screen is now pictured with its panels and fields.
- Admin-surface confidence rises from ~60% to ~75%: the menu and the run-time controls are known; the setup forms remain inferred from the string catalog.
- No change to numeric behaviour (~30%): still no bot internals.

## 5. Recommended use
- Treat `demo_23min_transcript.md` plus frames `01`–`19` as the acceptance script for the player UI: a tester should be able to follow Josh's year one click-for-click in the clone.
- Use frames `20_admin_*` as the wireframe for the admin console's run-time view.
- Pair with `resourcestrings_en.json` to name every label exactly.
