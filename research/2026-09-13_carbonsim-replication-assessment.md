# CarbonSim replication assessment

Date: 2026-09-13
Question: What do we already have, what is publicly available, what is still missing, and how confident can we be in a full replication of the "CarbonSim" tool referenced in the background reports?

## 1. What CarbonSim is

CarbonSim is the Environmental Defense Fund's (EDF) multi-user, web-based emissions-trading-system (ETS) training game. Participants each run a virtual company (one or more emitting "units") that starts long emissions and short allowances, and must comply at the lowest cost across several virtual years by combining on-site abatement, government auctions, an exchange, and over-the-counter (OTC) trades. AI bots run every unassigned company. An administrator configures the ETS rules and drives the clock. Josh Margolis (formerly EDF) is the administrator/trainer for every exercise found, including Vietnam.

Only one of the six background reports mentions it: the April 2025 Final Report of the ETP/UNOPS technical assistance "ETS Training and Simulation in Vietnam" (VNEEC, South Pole, VETS consortium). The other five reports (impact modelling, CTX operating model, carbon labelling, recommendations) do not reference the tool, although the 2025-07 impact report contains Vietnam-specific sector, abatement-cost and price assumptions that can seed a localized scenario.

## 2. What we have in hand (project + harvested)

### From `background/20250418_Final_20Report_EN.md`
- Tool selection, localization scope (Vietnamese translation, VND currency at ~23,825 VND/USD, exercises actually run in USD), instance URL `https://sim3.carbonsim.org/`.
- Delivery model: six two-day trainings (Feb–May 2024 Hanoi/HCMC, later sessions), 600+ participants, theory day then hands-on simulation day.
- Operational lessons: parallel-group simulations were tried then abandoned for a single session; year pacing was adjusted after confusion over "limited simulation years"; OTC use was actively encouraged; virtual regulator announced cap cuts and price floors mid-game; leaderboard ranks by marginal cost of compliance.
- Recommendation for future versions: add modules where participants play the government (allocation method, free-allocation rate, revenue use).

### Harvested into `research/carbonsim-sources/` (this session)
| Folder | Contents | Value for replication |
|---|---|---|
| `edf-official/` | Specs sheet (Nov 2018), FAQ (17 pp, Nov 2017), Glossary (10 pp, Oct 2017), Cheat sheet (2019), Brochure | The de facto functional spec: every screen, parameter, rule, and known limitation |
| `vietnam-ta-materials/` | Trainer deck EN + VN (31 slides, Feb 2024), Vietnamese CarbonSim glossary PDF | Vietnam exercise parameters, schedule, and 3 real interface screenshots |
| `sim3-instance/` | Login page HTML, `resourcestrings_en.json` (883 UI strings), `locale.js`, login bundle | Complete UI label/message inventory including admin screens; reveals tech stack |
| `exercise-reports/` | Dominican Republic (UNFCCC, 2023, 40 pp with 10 screen figures and results tables), Mexico, Colombia, World Bank "Simulating Carbon Markets" | Worked examples of full runs: parameters, auction results per vintage, system totals, leaderboard |
| `vfbc-2022-drive/` | Vietnamese agenda, glossary, intro deck from a Dec 2022 forestry-sector run | Earlier Vietnamese localization; shows 6-year, two-group agenda |
| `screenshots/` | Rendered slides incl. dashboard/abatement/exchange screens | Visual reference for layout and charts |

### Mechanics now documented (from spec, FAQ, glossary, decks)
- **Time**: virtual years of 20–30 real minutes; unlimited years; admin can pause, halt trading, warn N seconds before halt, end year, end game. Game states: Pending, Running, Paused, PausedAfterAuction, TradingHalted, YearEnded, GameEnded.
- **Allocation**: free allowances = X% of cap per sector, fixed for all years up front; cap declines at a set rate (Vietnam: 3%/yr, 90% free); sector BAU growth 2–6%/yr; sectors' emission shares must sum to 100%.
- **Auction**: sealed-bid, single-round, uniform-price; 4 per year by default, open ~45% of the year; multiple bids per player; ties broken by time; price floor/ceiling (Vietnam: $100/$300); current and future vintages; forward auctions; government reserve.
- **Exchange**: continuous order book; market, limit, stop-loss, partial-fill, fill-or-kill orders; separate books per vintage and for offsets; ±10% volatility band vs last trade; candlestick and close-price charts; live updates.
- **OTC**: seller sends an offer to a named player; buyer accepts/rejects; only sell-side offers exist (documented limitation); free-text messaging.
- **Abatement**: per-unit menu with upfront capital, annual reduction, implementation time, project lifetime, annual net revenue/cost, forecast ROI, cost per tCO2e; MACC chart; statuses building/operating/in-profit; reductions are additive (no interaction, documented simplification); temporary shutdown option; "Simulation" vs "Project lifetime" economics toggle.
- **Compliance**: end-of-year auto surrender (offsets first up to the offset limit, then allowances), banking capped at that year's obligation, excess forfeited; penalty = cash multiplier plus allowance debit (Vietnam: $300 + 1 allowance per missing tonne); admin can issue ad-hoc fines.
- **Finance**: large starting capital, overdraft/loan with interest, normal operating profit vs profit with abatement, net revenue from trades.
- **AI**: deterministic rule-based bots; majority of companies (Vietnam: ~206 bots vs ~36 human units); AI difficulty setting; AutoTrade toggle for human units; known artefact that bots can misprice offsets.
- **Scoring**: leaderboard by overall marginal cost of compliance, then final long/short; system-wide report of emissions, allowances sold/surrendered, revenue, offsets, abatement, penalties.
- **Admin**: sector and trading-system configuration, multiple parallel simulations, drag-drop systems into a simulation, registration PIN, currency and language management, glossary upload (XLSX), monitoring of players, real-time clock control.
- **Localization**: culture cookie `cultureName`; 9 languages on the VN instance; data-translation cultures for company names.

### Technology of the original (observed, not copied)
ASP.NET MVC 5.2 on .NET 4.0 / IIS 10; jQuery 1.11 + Bootstrap 3.2; SignalR for live push; dygraphs + techan.js for charts; DataTables; toastr. GUID keys suggest SQL Server. Proprietary, closed source; EDF terms prohibit reverse engineering and derivative works. Nothing on GitHub or in the academic literature reproduces it.

## 3. What is available further on the web (not yet pulled)
- YouTube: CarbonSim overview (54 min, `youtu.be/VdvWjN1J3jc`) and "open hand round" walkthrough (23 min, `youtu.be/SJpV_aNCSWY`) — the best remaining source for screen-by-screen UI behaviour.
- Wayback Machine copies of vncarbonmarket.com: the whole training-material library (day-1/day-2 lecture decks EN/VN, Vietnamese glossary, WB report). The live domain is now hijacked spam; do not link to it.
- EDF host-a-simulation page and Josh Margolis's contact (in the deck) — the only route to the admin manual, Vietnamese string file, scenario data and facility datasets.
- Comparable open tools for design ideas: ICAP "Carbono" board game, Motu's ETS game, agent-based carbon-market papers (arXiv 2406.07875 etc.) for bot logic.

## 4. What is still missing
1. **Server-side logic**: exact AI bot decision rules, auction tie-breaking details beyond "price then time", order-matching engine specifics, end-of-year reconciliation order of operations, penalty computation, capital/interest accrual.
2. **Datasets**: the 39 Vietnamese fictional firms with their sector, emissions, BAU growth, capital, and abatement menus; the sector split of the 355.85 Mt cap; offset supply; default AI difficulty settings.
3. **Vietnamese UI strings** (behind login) and the consultant's post-feedback parameter tweaks.
4. **Admin console UI** (only string keys and the Dominican report's descriptions exist; no screenshots).
5. **Full player UI** for Company Management, Unit Info, Surrender & Banking, System Info, Leaderboard (only dashboard, abatement, exchange, auction and OTC screens are pictured).
6. **Internal training guideline** (11 Oct 2023) and user guides/walkthroughs the report says were produced; not archived.
7. Any post-2018 changes: sim3/sim4 upgrades, the "parallel group" feature mentioned in the Final Report.

## 5. Confidence in replication (today)
| Layer | Confidence | Basis |
|---|---|---|
| Rules and mechanics (what the game does) | ~85% | Spec + FAQ + glossary + string catalog + two full exercise reports agree with each other |
| Player screens and information architecture | ~70% | Left-frame nav, top bar, dashboard, abatement, exchange, auction, OTC documented and partly pictured; 3 screens unpictured |
| Admin/config surface | ~60% | Every parameter is named in the spec and strings; layout and workflow only described |
| Exact numeric behaviour (bot bids, price paths, scoring identical to the original) | ~30% | Internal algorithms and data are private; can only be approximated |
| Pixel-level or code-level identity | 0% and not advisable | Closed source, ToS forbids it; a behavioural clone is the realistic target |

Overall: a **functionally faithful clone that a former participant would recognise and that supports the same training agenda is achievable with high confidence (~80%)**. An **exact replica** of EDF's software is not achievable from public material and would breach their licence; that gap is filled either by designing our own bot logic and datasets (using the 2025-07 impact report's Vietnam cost curves) or by negotiating access with EDF/Josh Margolis.

## 6. Recommended next steps
1. Watch and transcribe the two YouTube videos; capture every screen transition into a screen inventory.
2. Contact EDF/Josh Margolis: ask for the admin manual, Vietnamese string export, and permission or licensing; this alone could lift the data/logic confidence from 30% to 80%.
3. Write `activeContext.md` with a phased build plan (engine + rules first with tests, then player UI, then admin, then bots, then Vietnamese localisation) once the tech stack is chosen.
4. Seed the Vietnamese scenario from the 2025-07 impact report (sector MAC curves, price corridors) and the 39-firm list captured from the login page.
