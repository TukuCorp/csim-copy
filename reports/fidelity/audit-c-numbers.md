# Audit C - numeric fidelity (2026-10-21)

Scope: scenario parameters, market outcomes and bot behaviour of the clone against the public
sources. Read-only on code; the suite was not run (low memory). Clone numbers come from
`tests/CarbonSim.Engine.Tests/Golden/fidelity-golden.md` (seeded 242-unit, 3-year run, Normal unless
stated), `scenarios/vietnam-2024.json`, `tests/CarbonSim.Engine.Tests/FidelityRun.cs` and
`src/CarbonSim.Engine/Bots/ComplianceBot.cs`.

Source keys used below:

| Key | Source |
|---|---|
| DECK | `research/carbonsim-sources/vietnam-ta-materials/8-CarbonSim-Simulation_Joshua_EN.pdf` (slide n) |
| DEMO | `research/carbonsim-sources/youtube/demo_23min_transcript.md` [mm:ss] and `youtube/frames/*.jpg` (the 242-unit Mexico-dataset run, year 1) |
| VID | `research/2026-09-13_carbonsim-video-insights.md` (lecture notes from the 54-min overview) |
| DR | `research/carbonsim-sources/exercise-reports/2023-11-dominican-republic-exercise-report.md` (line) |
| MX | `research/carbonsim-sources/exercise-reports/mexico-simulation-general-report.pdf` (third simulation) |
| FAQ / SPEC / CHEAT | `research/carbonsim-sources/edf-official/CarbonSim-FAQ-2017.pdf` (question #), `CarbonSim-Specs-2018.pdf`, `CarbonSim-CheatSheet-2019.pdf` |
| T9 | `research/carbonsim-sources/etp-official/20250317_ETS_Task-9_Technical-Report_EN_clean.pdf` |
| FR | `background/20250418_Final_20Report_EN.md` |
| BRIEF | `research/2026-09-13_bot-logic-and-numeric-fidelity-options.md` §6 |

The Colombia and World Bank reports, the ETP presentation, T9 and FR carry no numeric exercise
outcomes (checked). No Vietnam session has published prices, compliance rates or leaderboards; every
outcome benchmark below is from the Mexico dataset that the Vietnam instance reused (same 355,850,000 t
cap; DECK slide 23 shows Mexican/Indian unit names; DECK slide 27 shows a "MEXICO" offsets product;
the DEMO admin screen reads "Choose System: Mexico").

## Top-10 gaps, ranked by impact x confidence

| # | Gap | Source | Clone | Impact | Confidence |
|---|---|---|---|---|---|
| 1 | **Abatement delivered is ~15x below the original in a 3-year run.** This, not the cap arithmetic, is why full compliance is unreachable. | DECK slide 30: 3-year term, year 3 to date: abatements 106.7 Mt (10.3% of the 1,035.8 Mt cumulative cap). DR l.1389: 479.3 Mt over 6 years (20.4% of emissions), 109.6 Mt in year 6 (25.6%). MX: 458-485 Mt over 6 years | 0 / 3.56 / 3.56 Mt delivered (golden supply table), 7.1 Mt cumulative (0.7%). No bot builds beyond rung 1 in any year | High | High (slide 30 figure is "to date": the 10-year column reaches 2.24 B, which can only be cumulative) |
| 2 | **Offsets are oversupplied ~3x.** The harness hands out 3% of emissions to everyone plus 25% to every fifth company, which is 8% of emissions a year, re-granted yearly. Its own comment says it targets 2-3% | DEMO frame 18: 9,289,410 t surrendered in year 1 = 2.61% of cap. DECK slide 30: 24.4 Mt over 3 years = 2.36% of cumulative cap. MX: 33-43 Mt over 6 years (~1.7-2.2%). DR l.1386: 21.5 Mt over 6 years (1.3% of surrenders) | 25.5 / 26.9 / 26.9 Mt (7.18 / 7.78 / 8.02% of cap); 79.3 Mt cumulative (7.65%). `FidelityRun.cs:129-130,244-266` | High | High |
| 3 | **Year-1 auction undersubscribed and pinned to the floor; bots never buy forward or bank on purpose.** | DEMO frames 11 and 18: 46,605,567 t sold in year 1 (35.3 Mt current + ~11.3 Mt forward), every lot 100% awarded, auction average 123.55 (revenue 5,758,187,714 / volume). Year-2 vintage lot of 6,689,980 t in auction 1, 100% sold | 24.0 Mt sold of 42.5 Mt offered (56%), average exactly 100.00. Bots skip forward lots (`ComplianceBot.cs:351`). 18.5 Mt goes back to the reserve and never returns (reserve share 0.0) | High | High |
| 4 | **Compliance: 91 units penalised in year 1 where the original had none, and 40% compliant company-years in total.** Most of the penalised count comes from small residual shortfalls, not scarcity | DEMO [21:23] and frame 18: year 1 had 0 penalties and 0 value with 242 units. MX: 74/76/84% compliance (humans, shocked 6-year run). DR: 383 unit-penalties over 6 years, 223 of them in year 6 (l.1392); shortfall 118.1 Mt = 5.0% of 6-year emissions | Normal: 294 of 726 (40.5%); by year 62 / 47 / 12% compliant. Year 1: 91 units short 394,672 t in total (~4,300 t each). Hard year 1: 23 units short **9 t in total**. Total shortfall 28.7 Mt = 2.6% of obligations, which is lower than DR's 5.0% | High | High |
| 5 | **Envelope 2's "year 3 >= 200" is not grounded for a 3-year unshocked run.** It is the test that makes the envelopes "mutually exclusive" | BRIEF §6 cites "Dominican pattern 150 -> 300". But DR's floor was 150 and was raised to 175 (auctions clear at 150 in years 2-3 and 175 in years 4-5; DR l.1298-1340), the run was 6 years, and 300 was reached only in the last auctions of years 5-6. MX also shocked the floor 100 -> 175. DECK slide 27 (3 years, 90% free) shows the exchange rising about 30-50% over the run, not to the ceiling. DEMO year-1 average was 123.55 | `FidelityTests.cs:109` requires >= 200. The clone reaches 255.97 by holding supply scarce, which is what forces the oversupply of offsets in gap 2 | High | Medium-high |
| 6 | **Exchange price is flat while the auction climbs** | MX: regulated-market prices "gradually climbed throughout all 6 years" (A 50-300, B 100-350, C 100-260). DEMO: in year 1 the exchange sits just below the auction (last trade 89.93, fills 98.35-99.17, against an auction average of 123.55). DECK slides 27-28: exchange close prices rise over the run | Exchange 99.33 / 101.80 / 104.46 against auction 100 / 181.84 / 255.97, so the exchange is 59% below the auction in year 3 | Medium-high | Medium |
| 7 | **Abatement menu shape and timing differ; year-1 delivered abatement is 0, and envelope 5 passes only because it measures committed tonnes** | DEMO frame 05 (one power unit): 8 options, build times 0.75-5 yr, cost 19.84 to ~600 $/t on the default "Simulation" basis. DECK slide 29 (one cement unit): 5 options from -14.36 to 463.22 $/t, build times 0.67-1 yr, large positive net revenues. DEMO frame 18: 2,947,644 t (0.83%) deducted from year-1 emissions | 3 rungs per unit at ~5 / 130 / 240 $/t (lifetime basis); `ImplementationYears` is an integer of 1-2, so nothing operates in year 1. `FidelityRun.cs:287-289` counts the annual reduction of projects implemented in year 1, not tonnes delivered | Medium | High |
| 8 | **Bots build projects in the last year that never operate** (open item 2) | FAQ #4: "Do not implement an abatement that will produce no reductions before the end of the simulation"; such options lose their cost or leave the table | `ComplianceBot.cs:303` `ImplementationYears > remainingYears` allows a 1-year build in year 3 of 3. Wasted capital feeds the bot cost of compliance | Medium | High |
| 9 | **Forward-vintage schedule** | DEMO frame 11: forward lot (vintage 2) in year 1 **auction 1**, sized 6,689,980 t = 19.4% of year-2 auctionable. DR l.1298-1340: forward lots of vintages 4-6 sold in years 1-5 auctions 1-3 | `forwardVintageShare` 0.20 (the size matches), but only the next vintage, and only in the year's **last** auction | Medium | Medium |
| 10 | **Capital and credit scale** | DEMO frame 03 (Baja California Power, about 1.5 Mt BAU): capital 119.2 M (~79/t), overdraft 92.4 M (~61/t) at 7%, N.O.P. 30.8 M (~20/t). DR l.1123 refinery: capital 717.9 M, overdraft 464.6 M. MX: "not strand too much capital ... could leave the capital without the needed capital to purchase DEMs", so cash can bind | 500/t capital, 250/t overdraft (half of capital), 40/t operating profit. Cash is deliberately never binding (scenario notes) | Low-medium | Medium |

Also noted, lower rank: the term is 3 years against the deck's 5-6 for the competitive day (it matches
the day-1 practice run, DECK slide 18); the offset-discount envelope (5-25%) is narrower than FAQ #17
(5-50%) and MX shows a premium (-6.5%), which the FAQ blames on AI logic; the offsets skip text quotes
28,143,193 t / 7.91%, but the golden now reads 25,541,458 t / 7.18%; 8.7 Mt of offsets stay unused in
year 3 while the fleet is 25.9 Mt short; 3.84 Mt is banked at the end of the final year, which is
waste (MX strategy 9: "avoid ending the simulation on a long position").

## 1. Parameter table

| Parameter | Source value | Citation | Clone value | Verdict |
|---|---|---|---|---|
| Cap year 1 | 355,850,000 t | DECK slide 19; MX; DEMO frame 18 | 355,850,000 | MATCH |
| Cap decline | 3%/yr (15-18% over 5-6 yrs) | DECK slide 19; MX ("18% over six years", later shocked to 22% and 24%) | 0.03 | MATCH |
| Free allocation | 90% | DECK slide 19; MX ("90% of their annual emissions") | 0.9 of each year's cap (320.3 / 310.7 / 301.3 Mt) | MATCH |
| Year-1 BAU vs cap | Forecast emissions year 1 = 355,850,000 = cap | DEMO frame 18 | Year-1 BAU totals exactly the cap | MATCH (the scenario notes call it a placeholder; the demo confirms it) |
| BAU growth | 2-6%/yr (MX: 2-5%) | DECK slide 19; MX | 2-6% per sector, measured fleet mean ~4% | MATCH |
| Price floor / ceiling | 100 / 300 | DECK slide 19; MX | 100 / 300 | MATCH |
| Auctions per year | 4 | DECK slides 9, 19; MX | 4 | MATCH |
| Auction open time | 45% of the year; 2:15 per auction in a 20-min year | DECK slide 19; VID (2 min 15 s each) | 2.25 min x 4 of 20 min = 45% | MATCH |
| Auction volume (current vintage) | 8,829,362 t per auction in year 1 (4 x = 35.3 Mt, 9.92% of cap) | DEMO frame 11 | 35,585,000 / 4 = 8,896,250 (10.0%) | MATCH (the source is 0.75% lower; perhaps a reserve holdback, which is not documented) |
| Forward vintages | Year-2 lot of 6,689,980 t (19.4% of year-2 auctionable) in auction 1; DR sells vintages up to 3 years ahead | DEMO frame 11; DR l.1298-1340; DECK slide 19 "Current + future years" | 20% of next year's auctionable, last auction only | DIFFERENT (timing and horizon; size matches) |
| Government reserve re-offer | Admin sets "Amount of allowances withheld"; no re-offer rule documented | SPEC | 0.0 | NOT-IN-SOURCE |
| Offset limit | 10% of obligation | DECK slide 19; MX (shocked to 7%, 2%, 20%); FAQ #24 | 0.1 | MATCH |
| Offset supply | Admin "Disburse Offsets"; year-1 surrender 9.29 Mt (2.6% of cap) | VID admin menu; DEMO frame 18 | Harness: 3% of emissions to all + 25% to every 5th company (= 8%/yr) | DIFFERENT |
| Banking limit | 100% of the year's obligation; allowances only; excess forfeited | DECK slide 19; FAQ #13-14 (MX: unlimited) | bankingLimit 1 | MATCH |
| Penalty | $300 + 1 allowance per missing tonne | DECK slide 19; MX | 300 + 1 | MATCH (golden: 7,776,585,374 / 25,921,951 t = 300.0) |
| Exchange volatility band | 10% around the last trade | DECK slide 19; MX; FAQ #16 | 0.1 | MATCH |
| Overdraft interest | 7% | DEMO frames 03, 11 ("Available Overdraft ($ @ 7%)"); DECK slide 29 shows 3% in another run | 0.07 | MATCH |
| Credit limit | Baja ~92 M on 119 M capital (77%); DR refinery 465 M on 718 M (65%) | DEMO frame 03; DR l.1123 | 50% of capital | DIFFERENT (minor) |
| Starting capital | ~79/t BAU (Baja); ~261/t (DR refinery); 9.9 bn (DECK slide 29) | as above | 500/t BAU | DIFFERENT (more generous; deliberately non-binding) |
| Operating profit | ~20/t BAU (Baja N.O.P. 30.8 M) | DEMO frame 03 | 40/t BAU | DIFFERENT (low impact) |
| Years | 5-6 (competitive); 3 on day 1 | DECK slides 18-19; DR (3 + 6); MX 6 | 3 | DIFFERENT for day 2, MATCH for day 1 (a trainer setting) |
| Year length | 20-30 min | DECK slides 9, 18, 19 | 20 | MATCH |
| Units / companies | 242 (~36 human, 206 AI); VID 38 human; SPEC up to 39 players | DECK slide 19; VID | 242 companies, 1 unit each, 37 human / 205 AI | MATCH on counts; the original allows several units per company (DEMO "Baja California Power Co. UNIT 1", DR "UNIT 72") |
| Sectors and shares | Real Vietnam split power 43.2 / cement 33.2 / steel 23.6% (264 facilities) | T9 table (97.76 / 75.13 / 53.37 Mt) | 43 / 33 / 24%, 60 / 90 / 92 units | MATCH on shares (to T9, not to the game); the game's own sector list (Mexico dataset: power, cement, refining, gas...) is NOT-IN-SOURCE for Vietnam |
| Abatement options per unit | 5-8 | DEMO frame 05; DECK slide 29 | 3 | DIFFERENT |
| Abatement cost range | -14.36 to ~600 $/t on the "Simulation" basis; several below the 100 floor (19.84, 88.89, -14.36) | DEMO frame 05; DECK slide 29 | ~5 / 130 / 240 $/t (lifetime basis) | DIFFERENT |
| Abatement build time | 0.67, 0.75, 1, 1.5, 2, 5 yrs | DEMO frame 05; DECK slide 29 | integer 1-2 yrs | DIFFERENT (no in-year delivery possible) |
| Abatement size | 47k-476k t per option, on units of ~1.5 Mt (3-30% each) | DEMO frame 05; DECK slide 29 | 1% / 5-10% / 12-20% of baseline | DIFFERENT (the original menus are deeper) |
| Abatement cost escalation | "as time passes, abatements gradually become more expensive" | FAQ #3 | none | NOT-IN-CLONE (no magnitude in source) |
| Allocation factors | Not documented | - | 1 unit in 10 at 1.5, rest 0.9444 | NOT-IN-SOURCE (clone invention to create sellers) |

The original's displayed cost per tonne, back-solved from the five rows of DECK slide 29 (a cement
unit, year 1 of a 3-year run, "Simulation" toggle), is (upfront - annual net revenue x T) / (annual
reduction x T), with T ~1.21 operating years left in the simulation. That reproduces -14.33, 89.2 and
447.6 against the -14.36, 88.89 and 446.20 shown. So the default view prices projects over the years
left to play, and the menus still contain several projects cheaper than the floor on that basis. The
clone moved the bots to the lifetime basis in QA round 5 because its menus have nothing cheap enough
on the simulation basis.

## 2. Outcome table

| Outcome | Source (citation) | Clone (golden, Normal) | Verdict |
|---|---|---|---|
| Year-1 auction sold | 46,605,567 t, every lot 100% awarded (DEMO frames 11, 18) | 24,007,538 of 42,488,490 offered (56%) | DIFFERENT |
| Year-1 auction average | 123.55 (DEMO frame 18) | 100.00 | DIFFERENT (envelope 2 band 100-110 excludes the source) |
| Auction average by year | 3-yr unshocked: not published. DR (6-yr, floor 150 -> 175): 150-338 per auction, year 6 251.46, run 176.71 (l.1298-1387) | 100.00 / 181.84 / 255.97 | NOT-IN-SOURCE for 3 years; the 200 threshold is extrapolated (gap 5) |
| Exchange price | Year 1 ~89.9-99.2 (DEMO [12:15]-[13:15], frame 11); rises over the run (MX; DECK slides 27-28) | 99.33 / 101.80 / 104.46 | Year 1 MATCH; later years DIFFERENT (flat) |
| Average allowance price, whole run | MX 160 / 219 / 179 (6-yr, shocked); DR 176.71 | 155.58 (3-yr) | Broadly comparable; term differs |
| Average offset price | DEMO year 1 92.63; DR 148.65; MX 173-213 | 141.57 | Within the source range |
| Offset discount | DEMO 25.0%; DEMO live 3-5% (VID); DR 15.9% (yr 6 15.0%); MX -6.5% (premium); FAQ #17 "5-50%", premium possible from AI logic | 9.0% (Hard -2.1%) | MATCH (Normal); Hard's premium has a precedent in MX |
| Later vintage premium | DECK slide 27: the year-2 vintage opens ~48-49 while year 1 trades ~45 (same date) | By-vintage run averages 99.80 / 151.87 / 207.46, which mostly reflect when each vintage trades | Passes the test, but the metric does not measure a same-date premium |
| Abatement, year 1 | 2,947,644 t delivered = 0.83% of cap (DEMO frame 18) | Delivered 0; committed 3,558,500 (1.00%) | DIFFERENT in what is measured; the envelope passes on committed tonnes |
| Abatement, cumulative | 3-yr: 106.7 Mt (10.3% of cum. cap; DECK slide 30). 6-yr: 479 Mt DR, 458-485 Mt MX (~20%+) | 7.1 Mt (0.7%) | DIFFERENT (~15x) |
| Offsets surrendered | Yr 1 9.29 Mt (2.61%; DEMO). 3-yr 24.4 Mt (2.36%; DECK slide 30). MX 33-43 Mt / 6 yr. DR 21.5 Mt / 6 yr | 25.5 / 26.9 / 26.9 Mt (7.2-8.0%/yr; 7.65% cum.) | DIFFERENT (~3x) |
| Penalties, year 1 | 0 units, $0 (DEMO frame 18, [21:23]) | 91 units, 118.4 M, 394,672 t (Hard: 23 units, 9 t) | DIFFERENT |
| Compliance, multi-year | MX 74 / 76 / 84% (range 54-96%; humans, shocked); DR practice year 3 80% of teams (l.413); DR competitive 383 unit-penalties / 6 yrs (223 in yr 6) | 294 / 726 = 40.5% (Hard 52.3%) | DIFFERENT |
| Shortfall volume | DR 118.1 Mt over 6 yrs = 5.0% of emissions (35.44 bn / 300); yr 6 87.6 Mt = 20% | 28.7 Mt over 3 yrs = 2.6% of obligations; yr 3 25.9 Mt = 6.8% | Volume MATCHES the source's order of magnitude; the count does not (many tiny shortfalls) |
| Leaderboard spread (bots) | 3-yr Mexico bots +31 to -40 $/t (VID lecture). DR all players -27.95 to 96.46 (bank excluded). MX 2.88-154.16. DEMO year 1 leaders 9.55, 11.53 | -62.64 to 39.72 (Hard -65.97 to 42.47) | MATCH on the top end; the bottom is more negative than -40 |
| Trading volume by channel | Auction yr 1 46.6 Mt (DEMO); DR 212.4 Mt over 6 yrs, 24.9 Mt in yr 6. Exchange and OTC volumes not published | Auction 24.0 / 25.3 / 26.8 Mt; exchange 9.9 / 15.1 / 12.6 Mt; OTC 0 (bots never use it) | Auction DIFFERENT (yr 1); exchange NOT-IN-SOURCE; OTC by bots matches FAQ #26 (OTC is seller-initiated, between humans) |
| Year-1 surrender identity | Allowances surrendered 343,612,946 = emissions - abatement - offsets (DEMO frame 18), so year 1 is exactly covered | Year 1 banked 10.5 Mt while 91 units were short | DIFFERENT (the fleet is long overall but badly distributed) |

## 3. The two skipped envelopes: root cause

### 3a. Full compliance (envelope 1)

Is the envelope grounded? Partly. "100% bot compliance, zero penalties" comes from the DEMO year-1
system report (0 units, $0) and the lecture's statement that the bots finish the 3-year Mexico
scenario between +31 and -40 $/t. No source shows a multi-year run with zero penalties. MX (74-84%)
and DR (383 penalties, 223 in the final year after the cap shocks) show real non-compliance in later
years, although those runs mixed humans with bots and were shocked. A defensible envelope: **zero
penalties in year 1; at least 74% compliant company-years over the run; total shortfall at most ~5% of
obligations** (the DR figure).

What makes the shipped run miss it, against the sources:

1. **Abatement supply (dominant).** activeContext ("Bot calibration", 2026-10-04) puts the 3-year gap at
   68 Mt (1,104.0 Mt owed against 1,035.8 Mt of allowances) and treats abatement as capped at about
   1% of the cap a year by the year-1 envelope. The source does not cap it that way. Year 1 is ~0.8%
   (DEMO), but 3-year cumulative abatement reaches 106.7 Mt (DECK slide 30), which alone covers the
   68 Mt gap 1.6 times over. The clone's menus make that impossible. Rung 2 costs exactly 130 $/t on
   the lifetime basis in every sector (P2 1440/12 + 10; C2 1830/15 + 8; S2 2200/20 + 20), so a bot
   builds it only once its expected price exceeds 130 / 1.05 = 124. That price is only visible after
   a year-2 auction clears, and the abatement passes run at 1-4% and 5-15% of the year
   (`ComplianceBot.cs:64,68`). Year-2 auction 1 closes at 11.25% of the year, so most bots decide on
   the year-1 price (~100). By year 3, a 1-year build can no longer operate. Result: nobody climbs the
   ladder (the abatement column is flat at 3,558,500 for years 2 and 3).
2. **Residual shortfalls (most of the penalised count).** Normal bids 0.9 of its position spread over
   the remaining auctions (`ComplianceBot.cs:345`), and its late settle pass is drawn at 60-80% of the
   year (`:70`), before auction 4 (75-86%). After auction 4 there is no pass to buy the last 10%.
   Whole-tonne flooring (`:379`) leaves one-tonne holes, which is how Hard has 23 penalised units for
   9 t in year 1. These residuals turn a fleet that banked 10.5 Mt in year 1 into 91 penalised units.
3. **Stranded offsets.** In year 3, 35.6 Mt of offsets are held, but only 26.85 Mt are surrendered,
   while the fleet is 25.9 Mt short. The lumpy grant (28% of emissions to every fifth company) puts
   offsets with companies that can use only 10%, and the surplus does not reach the short companies
   before year end.
4. **No deliberate banking or forward buying.** The original fleet bought 46.6 Mt in year 1, about 23
   Mt more than year 1 needed, by taking the forward lot and banking (DEMO). FAQ (via BRIEF §2) says
   bots "bank up to the obligation"; MX strategy 1 is "buying future year DEMs in auctions". The clone
   ignores forward lots (`:351`), and 18.5 Mt of year-1 supply goes back to the reserve for good.

The "mutually exclusive" finding in activeContext (QA round 4/5, "Scenario rebalance ... blocked") is
true only under two of the clone's own premises: year-3 clearing must reach 200 (gap 5, not grounded),
and abatement stays at ~1% a year (not grounded past year 1). Under source-grounded envelopes, the
supply arithmetic closes: 1,035.8 Mt of allowances + ~25 Mt of offsets (2.4%) + 45-110 Mt of
abatement covers 1,104 Mt, with years 1-2 long and year 3 short. That is the shape the original shows:
banking in year 1, prices rising toward year 3.

### 3b. Offsets 2-3% of the cap (envelope 5b)

Is the envelope grounded? Yes, well. DEMO year 1: 2.61%. DECK slide 30, 3-year run: 2.36% of
cumulative cap. MX: ~1.7-2.2% over 6 years. DR: 1.3% of surrenders. Treat 2-3% as a run-wide share
of cap (cumulative), checked in year 1 as well.

Why it is missed: it is a harness setting, not a property of the engine or the bots. `FidelityRun.cs`
grants 0.03 + 0.25 x 1/5 = 8% of emissions every year (granted 28.4 / 32.1 / 35.6 Mt). Its comment,
"sized so the fleet surrenders the 2-3% of the cap", is wrong by about 3x. The recorded reason for
keeping it (attempt 2: offsets 2.8% leaves the leaderboard maximum at 55.96 and 694 short
company-years) holds only because nothing replaces the missing offsets. With abatement sized to the
source (3a.1), the offsets cut becomes affordable. The skip text is also stale (it quotes 28,143,193 t
/ 7.91%; the golden is 25,541,458 t / 7.18%).

### 3c. Proposed changes (minimal, in order; each moves the golden unless marked)

| # | Change | Where | Expected effect | Risks / what it moves |
|---|---|---|---|---|
| C1 | **Re-ground the envelopes** (test-only, golden unchanged): E1 = 0 penalties in year 1, >= 74% compliant company-years, shortfall <= 5% of obligations. E2 = year-1 auction average 100-130 (DEMO 123.55); year 3 > year 1 and below the ceiling, dropping >= 200 (or, at most, >= 130 if a rise floor is wanted: DECK slide 27 shows ~+30-50%). E5a = abatement measured as tonnes delivered: year 1 0.5-2%, and 3-year cumulative >= 5% of cumulative cap (DECK slide 30 shows 10.3%). E5b = offsets 2-3% of cumulative cap. E3 optionally widened to the FAQ's 5-50%. Fix the stale skip text and the FidelityRun comment | `FidelityTests.cs:78,99,109,129,132`; `FidelityRun.cs:127-128,287-289` | Removes the artificial conflict. E5a becomes measurable on the shipped run (today it reads 0% delivered in year 1, so that test would then fail honestly) | The price-path test loses its ">= 200" teeth. Record the reason (DR's 150 floor and shocks, slide 27) in the test |
| C2 | **Offsets disbursement to ~2.6% of emissions**: `offsetShareOfEmissions` 0.03 -> 0.02, `lumpyOffsetShare` 0.25 -> 0.03 (2% + 3% to every fifth = 2.6%). This keeps a small seller group so offsets still trade | `FidelityRun.cs:129-130` (and the same defaults in `tests/CarbonSim.Data.Tests/TrainingRun.cs:74-104`; the host's disbursement advice if any) | Offsets surrendered ~2.3-2.6% of cap (E5b holds). Year-1 auction demand +~16 Mt, so year 1 becomes nearly fully subscribed and clears above the floor (toward DEMO's 123.55); no more stranded year-3 offsets | Alone, it worsens compliance and lifts the leaderboard maximum (attempt 2: 55.96). Ship it only together with C3 |
| C3 | **Re-author the menu's lower rungs toward the source shape**: rung 1 keeps ~1% of baseline but builds in the same year (needs `ImplementationYears` 0, or fractional build times pro-rated in the year, as in DEMO's 0.75-yr retrofit). Rung 2 drops from 130 to ~80-90 $/t lifetime (DECK slide 29 shows 88.89 and -14.36; DEMO 19.84 and 106.66) at ~6-8% of baseline with a 1-year build | `scenarios/vietnam-2024.json` sectors[*].abatementOptions (P1/C1/S1, P2/C2/S2) and its notes | Bots build rung 2 in year 1 (reservation 105 > ~85), delivering ~22-29 Mt a year in years 2-3 (~45-55 Mt cumulative, ~5% of cum. cap, half of slide 30). With C2 this closes the 68 Mt gap; year-1 delivered abatement ~1% (DEMO 0.83%) | Years 2-3 become less tight, so the auction path flattens (acceptable under C1, not under the old E2). The bot cost of compliance falls and may drop E6's maximum below 5. If the original's "Abatement Undertaken" turns out to be committed rather than delivered tonnes, committed year-1 abatement of ~7-9% would overshoot it (open question, medium confidence) |
| C4 | **Bots buy forward / bank on purpose**: in an auction that carries a forward lot, bid for it up to a share (e.g. 0.5) of next year's forecast shortfall at roughly the current expected price. Offer the forward lot in auction 1, not only the last (`forwardVintageShare` timing) | `ComplianceBot.Bid` (`:351`); `AuctionSchedule` forward-lot placement | Year-1 auctions sell out (DEMO: 100% awarded); a genuine same-date vintage premium appears; 10-20 Mt carries into year 3, which closes the year-3 deficit without a reserve re-offer | activeContext recorded that banking softened year 2 back to the floor. Under C1 that is allowed. This one moves the golden the most, so do it last |
| C5 | **Close the residual-shortfall holes**: bid the full remaining position in the year's last auction (`BidVolumeFraction` applies to earlier sections only), add a final exchange pass after the last auction closes (a trigger at ~0.88-0.95 of the year), and round the last bid up (`Math.Ceiling`) | `ComplianceBot.cs:345,379`, trigger table `:61-71` | Year-1 penalised units 91 -> ~0 (Hard 23 -> 0), which matches DEMO year 1; +100-200 compliant company-years | Slight upward pressure on auction 4; `The_sharper_bot_setting_comes_closer_to_full_compliance` must still see Hard < Normal (it should, through years 2-3) |
| C6 | **Decide abatement after price discovery** and fix the horizon: run `Abate` once more just after the year's first auction clears (event-driven, or a trigger drawn after 11.25% of the year) and change `ImplementationYears > remainingYears` to `>=` | `ComplianceBot.cs:221-237,303` | Mid rungs get built in year 2 when the auction shows the price (DR/MX "abate early"); no dead year-3 projects; the bot cost-of-compliance maximum falls | Keep `Quote` early: QA round 3 needs quotes in the first auction cycle |

Sequencing: C1 first (no golden move). Then C5 + C6 + the three final-review items in one
re-baseline. Then C2 + C3 together in a second re-baseline. C4 last, only if year-3 compliance still
misses after C2/C3. None of these has been run, so the expected effects are estimates from the supply
arithmetic in the golden, not measurements.

## 4. The three open golden-moving bot items (final review)

| Item | Does the original's documented behaviour support the fix? |
|---|---|
| 1. Escrow at year end (cancel resting orders and OTC offers before reconciling) | **Only in part.** FAQ #18 documents exactly the split the clone has: the header long/short "ignores escrow" (counts allowances resting in orders as held, like `Standing`), and surrender "includes escrow": it "will not allow you to surrender offsets or allowances that have been escrowed for trade", with a worked example ending at -45. A company whose allowances sit in a resting sell order can end short in the original too. DR l.1547 puts the burden on the player: "orders are good until cancelled ... cancel them as appropriate". So the engine-level "cancel everything before reconciling" would diverge from the source. Supported instead: (a) **bots** pull their own resting sells and offset offers before year end when they would otherwise be short (FAQ #7: AI cancels "illogical" orders; FAQ #8: AI acts rationally); (b) the **banking-cap leak**: FAQ #14 says surplus allowances over the obligation "will be forfeited", so apply the cap and forfeit to total holdings, escrowed included. Keep `ComplianceRegister` surrendering from available only |
| 2. Abatement horizon (`>` -> `>=`) | **Yes, explicitly.** FAQ #4: "Do not implement an abatement that will produce no reductions before the end of the simulation"; options whose build time exceeds the time left lose their cost or leave the table. The back-solved Simulation-basis cost on DECK slide 29 also goes to infinity as operating years left go to 0. Fix as proposed |
| 3. Bot double-buys offsets | **Yes, as rational behaviour**, but no source documents the original's exact order handling. FAQ #17 and #24 value offsets only up to the usage limit (surrender takes offsets first, up to the limit), and FAQ #8 says AI acts "rationally given a limited number of preset conditions". Buying past the limit is irrational. FAQ #17 admits the AI misprices offsets, which is a pricing artefact, not double ordering. Fix (count the resting offset bid in `Settle`); the numeric effect is small (slightly less offset demand, slightly more year-end surplus offsets) |
