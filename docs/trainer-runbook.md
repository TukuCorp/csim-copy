# Trainer runbook — the two-day CarbonSim exercise

This runbook is for the person driving the exercise from the trainer console. It covers the setup
that has to happen before the room arrives, the session-by-session flow of the two days and which
console control each step uses, what to point out on the player screens, and how to get a run back
after a crash. It is written for this clone; the two-day shape follows the Vietnam trainer deck,
the wording and steps here are ours.

## 1. What the room is doing

Teams of two or three people run a company of one or more emitting units inside a cap-and-trade
system. Their objective is to comply at the lowest cost: abate, buy at the government auction, buy
or sell on the exchange, trade over the counter, and bank or surrender at the year end. Most
participants in the room are rule-based AI companies, so the market is never empty even when only a
handful of teams have registered.

The exercise is a set of virtual years. Each virtual year is divided into one section per auction
(four by default). A section is a stretch of open trading followed by the auction window, so the
room always knows where it is: the progress strip counts the year, the auction and the time left.
The default year is 20 minutes long and the auction window is 2:15 at the end of each 5:00 section.

## 2. Before the room arrives

### 2.1 Host configuration

The host reads a CarbonSim section from configuration (appsettings.json, an environment variable, or
a launch profile). The keys a trainer cares about:

| Key | What it does |
|---|---|
| ConnectionString | The SQLite file the host keeps its accounts and saved exercises in. |
| ScenarioFile | The scenario the console offers to build an exercise from, relative to the content root. Ship it as scenarios/vietnam-2024.json. |
| RegistrationPin | The PIN a player must quote to register. Empty closes registration. |
| AdminEmail / AdminPassword | The bootstrap trainer account the host creates at boot. Leave empty and there is no way into the console. |
| EmailSender | Log writes reset codes to the log; InMemory keeps them in the process. |
| DisplayCurrency | USD (the engine's own unit, what the deck quotes) or VND. |
| VndPerUsd | How many dong one internal unit is worth when DisplayCurrency is VND. |
| StartDemoSimulation | Development only: starts the configured scenario at boot so the host has a live run to look at. Leave it off in the room. |

A training deployment that shows money in dong sets, for example:

    "CarbonSim": {
      "RegistrationPin": "<the room PIN>",
      "AdminEmail": "trainer@example.com",
      "AdminPassword": "<a password the trainer knows>",
      "DisplayCurrency": "VND",
      "VndPerUsd": 25000
    }

The engine keeps every amount in one internal unit and only the screens convert, both ways: with VND
on screen, players read every price and total in dong and type bid, order and offer prices in dong
too, and each price box names its currency. An export keeps the internal unit so a spreadsheet does
not have to guess. When you explain the collar from the deck, give it in the currency on screen
(100 USD is 2,500,000 dong at 25,000 to the dollar).

### 2.2 Build the exercise

1. Start the host (dotnet run --project src/CarbonSim.Web) and sign in with the trainer account.
2. Open **New exercise** (/admin/setup). The screen loads the configured scenario into an editable
   draft: the exercise name and seed, the rules (cap, reduction rate, free allocation share, offset
   limit, banking limit, penalty, collar, auctions per year, year length, auction window, volatility
   band, interest), the sectors and their abatement menus, and the growth band per sector.
3. Set the seed. The same seed reproduces the same AI behaviour, so pick a fresh one per room and
   write it down.
4. Check **Let players claim a company** and set the **Access PIN** for the room. Save the
   registration settings.
5. Press **Create the exercise**. The engine validates the whole draft and reports every problem at
   once; fix them and press again. The exercise is created in the waiting-to-start state.
6. Open it from the exercise list. The console shows the clock, the controls, and the parameters
   read-only. Parameters cannot be changed on a live exercise: the engine fixes the cap, allocation
   and abatement economics when the exercise is built, so a change means a new exercise.

### 2.3 Rehearse the room

Before the teams arrive, start the exercise, let one auction clear, then close the year and open the
next. That proves the clock, the auction and the reports work on the machine in front of you. Then
end the exercise or delete it and build the real one from the same seed.

Also open the player screens once in each language (the Tiếng Việt / English switch in the top bar).
Vietnamese is complete; the figures are grouped and the decimal separator is a comma, and the bid
and order boxes accept numbers written the Vietnamese way.

## 3. The two-day agenda

The shape below follows the deck: an hour of training, then a short practice run on day one, and a
fuller run with shocks and discussion on day two. Every virtual-year step is the same cycle of
abatement, four auctions, exchange trading, OTC, and a short debrief.

### Day 1 — learn the mechanics

| Time | Session | Console |
|---|---|---|
| 08:30 | Registration and sign-in | Registration open with the room PIN; watch teams claim companies on the exercise list. |
| 09:00 | Training hour: cap, allowance, offset, abatement, auction, exchange, OTC, compliance | No console action. Walk the player screens with a projector: dashboard, abatement, auction, exchange, OTC. |
| 10:00 | Practice year 1 (20 min) | **Start the year**. Let the first auction open and clear, then **Halt trading** with a short warning and **Close the year**. Debrief the leaderboard and the system report. |
| 10:25 | Practice year 2 (20 min) | **Open the next year**. Let the year run; point at prices moving and at the long/short bars. Close the year. |
| 10:50 | Practice year 3 (20 min) | **Open the next year**, then **End the exercise** at the close. |
| 11:15 | Debrief and questions | Reports and CSV. |

### Day 2 — run it for real

| Time | Session | Console |
|---|---|---|
| 08:30 | Recap and setup | A fresh exercise on a new seed, registration open. |
| 09:00 | Year 1 (30 min) | **Start the year**. Pause once to let the room read the auction result. Close the year. |
| 09:35 | Year 2 (20 min) | **Open the next year**. Use an **end-of-year modification** to cut or raise one company's emissions, or **Hand out offsets** to a company, and watch the price react. |
| 10:00 | Year 3 (20 min) | **Open the next year**. **Fine a unit** for a rule breach if you want the room to see a penalty land. |
| 10:25 | Year 4 and survey (20 min) | **Open the next year**, run it, close it, then **End the exercise** and take the survey. |
| 11:00 | Wrap up and awards | Leaderboard, system report, price report; export the CSVs. |

### Each virtual year, in order

1. **Abatement.** Teams open the abatement screen, read the MACC, and implement projects. An
   implemented project builds for its implementation time and then reduces emissions every year of
   its life; it cannot be undone.
2. **Auctions one to four.** The auction window opens near the end of each section. Teams bid a
   volume and a price inside the collar; all winners pay the same clearing price.
3. **Exchange.** Anytime while the clock runs, teams place market, limit and stop-loss orders, with
   partial fills and immediate-or-cancel, inside a volatility band around the last price (anchored on
   the auction floor before the first trade).
4. **OTC.** A team can send a named company an offer to sell; the other side accepts or rejects.
5. **Debrief.** Halt with a warning, close the year, and read the leaderboard and the system report
   before opening the next year.

## 4. Driving the clock and the market

Everything is on the exercise page (/admin/run/{id}).

- **Start the year** / **Open the next year** — begins the year and grants its free allocation.
- **Pause the clock** / **Let the clock run** — freezes and resumes the year; nothing is lost.
- **Halt trading** — stops trading. Put a number of seconds in **Warn seconds before halting** and
  the room gets a warning first; leave it blank and trading halts at once. Halting is how a year
  ends: the year has to be halted before it can be closed.
- **Close the year** — runs the year-end reconciliation (offsets first up to the limit, then
  allowances by vintage), banks what may be banked, forfeits the rest, applies penalties, and saves
  the run.
- **End the exercise** — available once the last year has been closed.
- **Player messaging** — the on/off switch. Turn it off during a debrief so the room stops typing.
- **Fine a unit** — a cash fine with a reason, for a rule breach you want the room to see.
- **Hand out offsets** — gives a company a volume of offsets, the way a regulator seeds the offset
  market. Do this early in a year if you want offset trading in that year.
- **Add abatement for a unit** — implements a project on a team's behalf, for a teachable moment.
- **End-of-year modification** — writes a change in emissions (negative to cut) into one unit's
  business-as-usual path for a chosen year, so a shock survives a save and reload. It cannot be
  undone or listed separately, so keep a note of what you applied.
- **Automatic trading** — one switch per unit. Turning a human unit on lets the AI run it; turning
  an AI unit off makes it passive. The switch rebuilds the AI fleet, so a bot's progress is dropped
  when you use it.

## 5. What to point out on the player screens

- **Dashboard** — finance, compliance, the long/short bar, abatement status, auction and trade
  history. This is the screen to put on the projector at a debrief.
- **Abatement** — the MACC shows cost per tonne from left (cheap) to right (dear); the status box
  shows what is building, operating, in profit and complete.
- **Allowance auction** — the bid form shows the collar and the capital the bid would commit; the
  vintages this auction are listed, and forward vintages are marked as a forward sale.
- **Exchange market** — the price chart is one candle per virtual year with the auction floor as a
  dashed line; the top of the book, the last ten trades, and the order form sit beside it.
- **OTC market** — offers waiting for an answer and the team's own offers.
- **Surrender and banking** — the obligation, what was surrendered, what was banked, what was
  forfeited, and the penalty if any. Surrender happens automatically when the year closes.
- **Leaderboard** — ranked by marginal cost of compliance, so the cheapest compliers are at the top.
  Negative outliers are teams that were paid to abate.
- **System info** — the whole system's totals this year and to date, including the government's
  auction revenue and reserve.

## 6. Reports and CSV

The **Reports** page (/admin/run/{id}/reports) renders five reports, each with an **Export as CSV**
link. The same files are available directly:

| Report | Path |
|---|---|
| System totals | /api/admin/runs/{id}/reports/system.csv |
| Companies | /api/admin/runs/{id}/reports/companies.csv |
| Units | /api/admin/runs/{id}/reports/units.csv |
| Prices by year | /api/admin/runs/{id}/reports/prices.csv |
| Leaderboard | /api/admin/runs/{id}/reports/leaderboard.csv |

The CSV is behind the trainer account and writes numbers without grouping, in the engine's internal
unit, so a spreadsheet reads them as numbers. The surrender status page
(/admin/run/{id}/surrender) lists each company's obligation and penalty for the projector.

## 7. Recovery

- **A run that is under way lives in memory.** The host saves it at each year end and after each
  control, so a crash loses at most the current year. The saved copy appears under **Saved
  exercises**; press **Load** to bring it back with its own clock, markets and bots. The live and
  saved copies of the same exercise are the same id, so loading a saved run that is still live is
  refused rather than overwriting it.
- **Restarting the host** keeps the database (accounts, claimed companies, saved runs) and loses the
  in-memory live runs. Start the host, sign in, load the saved exercise, and resume from the year it
  was saved at. Notices for an auction that was open at the crash are in-memory only, so that one
  auction's opening or closing notice may be re-sent once after a reload; the auction itself is
  intact.
- **Registration problems.** If a team cannot register, check the room PIN on **New exercise**, that
  registration is still open, and that the company they want has not been claimed already. A
  password reset code goes wherever EmailSender sends it.
- **A company with no player.** Any company not claimed stays an AI company and keeps trading.
- **A run that will not start a year.** A year can only start from waiting-to-start or year-ended;
  pause, halt or close the year first. Creating a second exercise on a seed already under way is
  refused, so change the seed.
- **Wrong currency on screen.** The display currency is host configuration; restart the host with
  DisplayCurrency set as intended. The engine's numbers never changed.

## 8. Fidelity: what this clone promises, and what it does not

The clone is behaviourally faithful within the envelopes in the bot-fidelity brief, not numerically
identical to the original. The seeded three-year run of the shipped scenario currently meets the
envelopes for the year-one clearing price at the floor, the offset discount, later vintages pricing
above earlier ones, the year-one abatement share, and the leaderboard spread with negative outliers.
It does not yet meet full compliance at Normal difficulty, prices climbing to the ceiling by year
three, the 2-3% offset surrender share, or a mid-run price shock inside one virtual month, and those
four are recorded as skipped measurements rather than tuned away. Treat the price path and the
leaderboard as directionally right, and do not present a specific number as the original's.

