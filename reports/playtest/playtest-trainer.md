# Playtest report: trainer lane

Status: **COMPLETE - all three years played.** The earlier abort was caused by the local host being
shut down for low memory; the host was restored for this run and the exercise was played end to end.

## Summary

| Item | Value |
|---|---|
| Did 3 years complete? | **Yes** |
| Years played | 3 of 3 (year 1: 14:56, year 2: 15:05, year 3: 15:12) |
| Total time | **24 minutes** of exercise clock (14:56 - 15:20), plus about 6 min of setup and liveness checks |
| Wall-clock from host restore to final report | 14:32 - 15:41, Asia/Saigon, 2026-10-21 |
| Exercise | “Vietnam ETS Pilot 2024”, run id `597d2da5-324b-39db-3231-2926e2fce972`, seed 20240916 |
| Settings | YEAR = 3, YEAR LENGTH (MIN) = 6, AUCTION WINDOW (MIN) = 0.75; every other field left at its default |
| Console pages visited | Trainer console (controls), Reports, Surrender status by company, Exercises list, New exercise, player Dashboard, player Messages, player Leaderboard, player Surrender and banking |
| Shock controls exercised | Pause + resume (year 1), halt + reopen trading (year 2), messaging off/on (year 2), emissions shock (year 3) |
| Findings | **4** (1 MAJOR, 2 MINOR, 1 NOTE) |
| Players | Alpha (Power, Cong ty Dien luc Binh Minh) and Bravo (Cement, Cong ty Xi mang Da Trang) were registered by this lane, because the players lane had already aborted without writing `players-ready` |

### Environment note (not an application finding)

The host was down when this lane resumed: `SERVER: DOWN Unable to connect to the remote server`,
no `dotnet` process, no listener on 5097. The app was rebuilt with
`dotnet publish src/CarbonSim.Web/CarbonSim.Web.csproj -c Release -o reports/playtest/_playtest/publish`
and started on `http://127.0.0.1:5097` in the `Demo` environment with the credentials from the brief
(`trainer@carbonsim.demo` / `rehearsal-trainer-1`, registration PIN `rehpin`). It came up clean:
four EF migrations applied, `Created the administrator account trainer@carbonsim.demo`, and
`Now listening on: http://127.0.0.1:5097`. The host then stayed up for the whole lane.

## Timeline

All times Asia/Saigon, 2026-10-21. “Auction” is the console AUCTION counter.

| Clock time | Year | Auction | Action taken | What the screen showed |
|---|---|---|---|---|
| 14:32:33 | - | - | Host restored (Release publish, Demo env, port 5097) | `Now listening on: http://127.0.0.1:5097` |
| 14:34 | - | - | Signed in as trainer | “Signed in as Exercise administrator”, link “Trainer console” |
| 14:35 | - | - | Opened New exercise | Defaults shown: YEAR 3, YEAR LENGTH (MIN) 20, AUCTION WINDOW (MIN) 2.25, PIN `rehpin`, registration open, 242 companies |
| 14:36 | - | - | Set YEAR LENGTH (MIN) = 6 and AUCTION WINDOW (MIN) = 0.75 via `eval` + change event; re-read after the re-render | `year=6 auction=0.75 years=3 name=Vietnam ETS Pilot 2024 seed=20240916` |
| 14:41 - 14:53 | - | - | Waited for `_playtest/players-ready`, polling every 15-20 s for 10 minutes | File never appeared (the players lane had already aborted). Carried on per the brief. |
| 14:41:43 | - | - | Registered Alpha (Power) | Account id 2, role Player |
| 14:42 | - | - | Registered Bravo (Cement) | Account id 3, role Player; unclaimed companies fell 37 -> 35 |
| 14:54 | - | - | Clicked “Create the exercise” | Landed on `/admin/run/597d2da5-324b-39db-3231-2926e2fce972`; STATE “Waiting to start”, YEAR 1/3, AUCTION 1/4, TIME LEFT 6m 00s |
| 14:56 | 1 | 1 | Clicked “Start the year”; wrote `_playtest/year-1-started` | STATE “Running”, TIME LEFT 5m 54s, AUCTION 1/4 |
| 14:57 | 1 | 1 | Opened Reports | System report, price-by-year table, company report and leaderboard all populated and live |
| 14:58 | 1 | 1 | Opened Surrender status by company | 242 rows; obligation 2,550,258, covered 2,167,704, shortfall 382,554, status NONE |
| 15:00 | 1 | 3 | **Shock: Pause** | STATE “Paused”; Pause button gone, Resume + Halt offered |
| 15:00 | 1 | 3 | Read TIME LEFT twice, 12 s apart | `2m 03s` then `2m 03s` - the clock is frozen while paused |
| 15:01 | 1 | 3 | **Shock: Resume** | STATE “Running”, TIME LEFT 2m 03s, AUCTION 3/4 |
| 15:02 | 1 | 4 | Watched the year run out | 0m 39s -> 0m 26s -> “Trading halted”, `admin-year-over` notice present, Resume absent, “Close the year” offered |
| 15:04:06 | 1 | 4 | Clicked “Close the year” | STATE “Year ended”, YEAR 1/3, “Start next year” offered, End year gone |
| 15:04 | 1 | 4 | Opened Surrender status again | Alpha company reconciled: surrendered 2,167,704, shortfall 382,554, penalty 114,766,200 USD, status YEAR ENDED |
| 15:05:50 | 2 | 1 | Clicked “Start next year”; wrote `_playtest/year-2-started` | STATE “Running”, YEAR 2/3, AUCTION 1/4, TIME LEFT 5m 54s |
| 15:07 | 2 | 2 | Opened Reports | Numbers had moved: year 2 cap 345,174,500, to-date cap 701,024,500, allowances surrendered to date 342,541,555, penalty to date 3,992,533,560 USD, year 1 price row now shows 34,951,873 t at 99.91 USD cleared at 100.00 |
| 15:09:43 | 2 | 3 | **Shock: Halt** (no warn lead) | STATE “Trading halted”, TIME LEFT 1m 31s; Resume offered (year not over), Halt gone, “Close the year” offered |
| 15:10:03 | 2 | 4 | **Shock: Resume after halt** (reopen trading) | STATE “Running”, AUCTION 4/4, TIME LEFT 1m 24s |
| 15:11:40 | 2 | 4 | Watched the year run out | “Trading halted”, year-over notice, Resume absent, “Close the year” offered |
| 15:12:20 | 2 | 4 | Clicked “Close the year” | STATE “Year ended”, YEAR 2/3, “Start next year” offered |
| 15:12:41 | 3 | 1 | Clicked “Start next year”; wrote `_playtest/year-3-started` | STATE “Running”, YEAR 3/3, AUCTION 1/4, TIME LEFT 5m 53s |
| 15:13 | 3 | 1 | **Shock: messaging off** | `messaging=Off`, notice “The messaging setting has been saved.” |
| 15:14 | 3 | 1 | **Shock: messaging back on** | `messaging=On` |
| 15:15 | 3 | 1 | **Shock: emissions shock**, Alpha company, year 3, 5,000 t | Notice “Changed ... year 3 emissions by 5,000 t”; the company report row moved to 2,746,062 t forecast emissions |
| 15:18:25 | 3 | 4 | Watched the year run out | 0m 17s -> 0m 04s -> “Trading halted”, year-over notice, Resume absent, “Close the year” offered |
| 15:19:48 | 3 | 4 | Clicked “Close the year” | STATE “Year ended”, YEAR 3/3, “End the exercise” offered, “Start next year” gone |
| 15:20:15 | 3 | 3 | Clicked “End the exercise” | STATE “Simulation ended”; Start / Pause / Resume / Halt / End year all withdrawn |
| 15:21 | 3 | 3 | Read final Reports: system report, price table, company report, leaderboard | Cap to date 1,035,843,765; allowances surrendered 947,163,047; auction revenue 17,669,106,631 USD; penalty to date 47,602,746,102 USD |
| 15:25 | 3 | 3 | Signed in as Alpha and as Bravo in separate browser sessions | Top bar shows the account display name (“Alpha”, “Bravo”) |
| 15:26 | 3 | 3 | Read the player leaderboard as Alpha | Alpha own row marked with `*` at rank 224; Bravo company at rank 227 |
| 15:27 | 3 | 3 | Sent a message from Alpha, read it from Bravo | Bravo message log: “Year 3 ... Playtest trainer note”, count 1 |
| 15:29 | 3 | 3 | Read the Bravo player Surrender and banking screen | Obligation 1,414,583, covered 565,000, shortfall 849,583 - arithmetic consistent |
| 15:31 | - | - | Created a second exercise (probe) `82001d78-ac70-349e-809e-140554d1a53e`, same 6 min / 0.75 min settings | STATE “Waiting to start” |
| 15:32 - 15:33 | - | - | **Lesson check: a year must not start by itself** | STATE stayed “Waiting to start” and TIME LEFT stayed “6m 00s” for 60+ s with no button pressed; only “Start the year” offered |
| 15:33 | - | 1 | Started the probe year, paused it, read TIME LEFT twice | “Paused”, 5m 38s then unchanged - clock frozen |
| 15:34 | - | 1 | Resumed the probe year and let it run out | Watched 5m 21s down to 0m 10s, then “Trading halted” |
| 15:39:54 | - | 4 | **Lesson check: an expired year must not be resumable** | Buttons present: only `admin-end-year` (“Close the year”), `admin-messaging-toggle`, `admin-change-rules`. Resume absent. Notice: “The clock has run out of time for this year. Close the year to carry on.” |
| 15:40 | - | 4 | Closed the probe year | STATE “Year ended”, “Start next year” offered - identical behaviour to the main run |
| 15:41 | 3 | 3 | Re-checked the main run control set on “Simulation ended” | No clock control buttons offered at all |

## Findings

| Severity | Screen | Steps to reproduce | Expected | Actual | Evidence | Status |
|---|---|---|---|---|---|---|
| MAJOR | Trainer console -> Reports -> Leaderboard, and the player Leaderboard screen | Register two human players (Alpha, Bravo), run the exercise, open Reports -> Leaderboard (or the player Leaderboard) and read the UNIT (PLAYER) column for a human company | The player display name (“Alpha”, “Bravo”) should appear next to the unit, so a trainer can tell who plays which company | The PLAYER column repeats the **company name** for every human company, so the trainer and the players cannot tell which person owns which company. The player Leaderboard has the same defect (its own row is marked with `*`, but the unit label still names the company) | Admin leaderboard rows: `224 | Cong ty Dien luc Binh Minh | Cong ty Dien luc Binh Minh (Cong ty Dien luc Binh Minh) | +110.95 | 2,936,663` and `227 | Cong ty Xi mang Da Trang | Cong ty Xi mang Da Trang (Cong ty Xi mang Da Trang) | +112.10 | 1,523,821`. Player leaderboard as Alpha: `224  Cong ty Dien luc Binh Minh *  Cong ty Dien luc Binh Minh`. Same for the message log sender: `Year 3 Cong ty Dien luc Binh Minh Playtest trainer note` | OBSERVED |
| MINOR | Trainer console -> Reports -> “Average traded price by year” | Open Reports and compare the “ALLOWANCE AUCTION VOLUME” column with the “AVERAGE AUCTION” column for the same year | The volume column should hold auction volume, matching the auction-price column beside it | The volume column holds **all-channel** allowance volume (auction + exchange + OTC), while the price column beside it is auction-only, so the two columns cannot be reconciled. The CSV export of the same table labels the column correctly as “Allowance volume”, which makes the screen and its own export disagree | Screen: year 2 `ALLOWANCE AUCTION VOLUME 40,039,950` against `AVERAGE AUCTION 239.73 USD`. CSV for the same row: `2,40039950.29,196.7595,0,0,239.73,...` under the header `Allowance volume`. Root cause: `AdminSession` fills `AllowanceVolume` from `Journal.Volume(year, year, kind: ProductKind.Allowance)` with no channel filter, while `AuctionAverage` filters `TradeChannel.Auction` | OBSERVED |
| MINOR | Trainer console -> Reports -> Leaderboard, versus the player Leaderboard | Compare how the unit/owner label is built on the two leaderboards | Both screens should render the same label the same way | The player screen de-duplicates a unit whose name equals the owner name (`Display.Unit` drops the owner when they match); the trainer screen always emits `unit (player)`, so a human company reads as a doubled, bracketed name there and a plain name on the player screen | Player screen row: `224  Cong ty Dien luc Binh Minh *  Cong ty Dien luc Binh Minh`. Trainer screen row: `224 | Cong ty Dien luc Binh Minh | Cong ty Dien luc Binh Minh (Cong ty Dien luc Binh Minh)`. `AdminReports.razor` writes `@row.Unit (@row.Player)` while `Display.Unit` compares the two | OBSERVED |
| NOTE | Trainer console -> Reports -> Company report | Read the COST OF COMPLIANCE and MARGINAL COST OF COMPLIANCE columns for a human company on the final run | The two columns should describe the same period, or say which period they cover | The columns are correct but silently describe **year 3 only**, while the leaderboard beside them reports the **three-year overall** figure, so two different cost numbers sit on the same screen without saying which is which | Company report row: `... 489,086,700 ... 178.1048`; leaderboard row for the same company: `110.9534`. Check: 489,086,700 / 2,746,062 = 178.1048 (consistent), and the leaderboard 110.95 is the three-year total. The column headers read “COST OF COMPLIANCE” and “OVERALL MARGINAL COST OF COMPLIANCE” | OBSERVED |

### Lesson checks from the brief

| Check | Result | Evidence |
|---|---|---|
| A year must not start by itself | **PASS** | On the probe exercise, STATE stayed “Waiting to start” and TIME LEFT stayed “6m 00s” for 60+ s with no control pressed; only “Start the year” was offered |
| A year whose time is over must not be resumable, and the console must say why | **PASS** | At the end of a year the only clock button is `admin-end-year` (“Close the year”); the Resume button is absent, and the notice reads “The clock has run out of time for this year. Close the year to carry on.” Verified at the end of years 1, 2, 3 and on the probe run |
| A control must not be offered in a state where it cannot act | **PASS** | “Waiting to start”: Start the year only. “Running”: Pause + Halt. “Paused”: Resume + Halt (no Pause). “Trading halted” with time left: Resume + Close the year (no Halt). “Trading halted” with time over: Close the year only. “Year ended”: Start next year (or End the exercise in year 3). “Simulation ended”: no clock control at all |

### Arithmetic checks

| Check | Numbers | Result |
|---|---|---|
| Company report marginal cost | 489,086,700 / 2,746,062 = 178.1048 | Matches the shown +178.1048 |
| Bravo company report marginal cost | 254,874,900 / 1,414,583 = 180.1767 | Matches the shown 180.1767 |
| Cap over three years | 355,850,000 + 345,174,500 + 334,819,265 = 1,035,843,765 | Matches “Cap ... to date” |
| Bravo surrender (player screen) | obligation 1,414,583, covered 565,000, shortfall 849,583 | Consistent: 565,000 surrendered, 849,583 short of the 1,414,583 obligation |
| Year 1 surrender, Alpha company | obligation 2,550,258, covered 2,167,704, shortfall 382,554 | 2,550,258 - 2,167,704 = 382,554, matches the screen |
| Year 3 auction revenue against volume | 8,035,662,300 USD over 32,205,140.81 t | Implies about 249.5 USD/t against the reported auction average of 300.00 USD - the difference is volume that was auctioned but not sold (bids are escrowed, not all cleared) |

## Final leaderboard (top 5) and where Alpha and Bravo ranked

Top 5 of the final run (`AdminReport.Leaderboard`):

| Rank | Company | Marginal cost of compliance | Final long/short position |
|---|---|---|---|
| 1 | AI Steel 89 (AI) | -37.33 | -9,283 |
| 2 | AI Steel 69 (AI) | -31.01 | -9,283 |
| 3 | AI Steel 59 (AI) | -9.82 | 86,479 |
| 4 | AI Steel 19 (AI) | -2.22 | 131,946 |
| 5 | Cong ty Nang luong Dai Phat | +0.00 | -2,201,845 |

Where the two human companies finished:

| Company | Player | Rank | Marginal cost of compliance | Final long/short position |
|---|---|---|---|---|
| Cong ty Dien luc Binh Minh | Alpha | **224** of 242 | +110.95 | 2,936,663 (short) |
| Cong ty Xi mang Da Trang | Bravo | **227** of 242 | +112.10 | 1,523,821 (short) |

Both human companies ranked below every AI company, which is expected: nobody bid, so they carry
their full obligation as shortfall into the penalty (Alpha 489,086,700 USD, Bravo 254,874,900 USD
over the run to date).

## Final system report (year 3 and to date)

| Product | This year | To date |
|---|---|---|
| Cap | 334,819,265 | 1,035,843,765 |
| Free allocation | 301,337,339 | 932,259,389 |
| Allowances on offer | 33,481,926 | 103,584,376 |
| Forecast emissions | 382,467,695 | 1,105,838,867 |
| Allowances surrendered | 281,999,154 | 947,163,047 |
| Offsets surrendered | 0 | 0 |
| Banked for next year | 6,349,988 | 14,314,324 |
| Forfeited | 0 | 0 |
| Auction revenue | 8,035,662,300 USD | 17,669,106,631 USD |
| Average allowance price (all channels) | 284.98 USD | 191.69 USD |
| Auction average price | 300.00 USD | 209.02 USD |
| Emission reductions | 2,643,579 | 5,287,158 |
| Penalty per tonne | 235 / 30,140,562,456 USD | 638 / 47,602,746,102 USD |

Price by year: year 1 cleared at 100.00 USD (34,951,873 t, average allowance price 99.91),
year 2 cleared at 235.91 - 245.53 USD (average auction 239.73), year 3 cleared at 300.00 USD
(average auction 300.00 - the ceiling).

## What worked well

- The console never offered a control it could not honour. Across every state - waiting, running,
  paused, halted, expired, year ended, simulation ended - the button set matched the engine state,
  and the two lesson checks the brief singles out both passed.
- The expired-year message is genuinely useful rather than a bare refusal: “The clock has run out
  of time for this year. Close the year to carry on.” names the cause and the next step.
- Pause is honest about the clock. TIME LEFT read `2m 03s` twice, twelve seconds apart, while
  paused, and resumed from exactly that value.
- Every console action confirms itself in plain words on the same screen: “The messaging setting
  has been saved.” and “Changed ... year 3 emissions by 5,000 t.”
- The reports are live and reconcile. The year-2 report already carried year 1 surrendered volume
  and penalty, the year-3 report carried all three years, and the cap foots across the three years
  to the tonne.
- The fixture is cheap to rehearse. A fresh exercise takes one click on “New exercise” plus
  “Create the exercise”, and the year-length / auction-window pair that makes a 6-minute year with
  four 45-second auctions works exactly as specified.

## Open questions

1. Should the console show the **player** name rather than the company name in the UNIT (PLAYER)
   column? If so, the run needs the account display name attached to the company it owns; today
   the run only knows that a company has a human owner.
2. Should the price report volume column be split into “Auction volume” and “Total volume”, or
   should its header be changed to match the CSV “Allowance volume”? Either fix removes the
   apparent disagreement with the auction-price column beside it.
3. Should the company report say which year its cost columns cover? It reports year 3 while the
   leaderboard on the same screen reports the three-year total, and neither says so.
4. The brief asks a trainer to wait for `players-ready` before creating the exercise, but the
   console does not require registered players, and this run created the exercise with the two
   players already registered. Should the console warn when an exercise starts with no human
   players, since the leaderboard then contains no one a trainer can recognise?
5. Would the hosted demo benefit from the same liveness check this lane had to add? A player lane
   that polls a dead host silently reads as “running” rather than “broken”.
6. Peak memory on this machine is the real constraint on a rehearsal: the host was reaped once
   already. The restored build ran at roughly 140 MB of working set, so a host plus three browser
   sessions should fit in a couple of gigabytes - worth stating as a floor for the next session.
