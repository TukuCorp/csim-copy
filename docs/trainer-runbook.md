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
(four by default), one quarter of the virtual calendar each. A section opens with its auction window
and the rest of it is open trading on the auction's result, so the room always knows where it is:
the top bar shows the virtual date and the progress strip counts the year, the auction and the time
left. The default year is 20 minutes long and the auction window is the first 2:15 of each 5:00
section, as in the original, where the year's first auction is open as soon as the year starts.

## 2. Before the room arrives

### 2.1 Host configuration

The host reads a CarbonSim section from configuration (appsettings.json, an environment variable, or
a launch profile). The keys a trainer cares about:

| Key | What it does |
|---|---|
| ConnectionString | The SQLite file the host keeps its accounts and saved exercises in. |
| ScenarioFile | The scenario the console offers to build an exercise from. The default, scenarios/vietnam-2024.json, is copied into every build, so it needs no setting. |
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

1. Start the host with `powershell -File scripts/run-demo.ps1` (docs/deployment.md, section 2). The
   banner prints the trainer sign-in and the room PIN. Sign in with the trainer account.
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
   implemented project builds for its implementation time, counted from the day it is bought (some
   take a quarter of a year, so they cut emissions in the year they are bought), and then reduces
   emissions for its life; it cannot be undone.
2. **Auctions one to four.** The auction window opens at the start of each section. Teams bid a
   volume and a price inside the collar for this year's vintage or next year's, see their bids
   listed, and can cancel one while the window is open; all winners pay the same clearing price. An
   alert on every screen gives the result when the auction closes.
3. **Exchange.** Anytime while the clock runs, teams place market, limit and stop-loss orders, with
   partial fills and immediate-or-cancel, inside a volatility band around the exchange's own last
   price (anchored on the auction floor before the first trade). The price chart draws one candle
   per virtual month.
4. **OTC.** A team can send a named company an offer to sell; the other side accepts or rejects.
5. **Debrief.** Halt with a warning, close the year, and read the leaderboard and the system report
   before opening the next year.

## 4. Driving the clock and the market

Everything is on the exercise page (/admin/run/{id}).

- **Start the year** / **Open the next year** — begins the year and grants its free allocation.
- **Pause the clock** / **Let the clock run** — freezes and resumes the year; nothing is lost.
- **Halt trading** — stops trading. Put a number of seconds in **Warn seconds before halting** and
  every player's screen counts down to the halt first; leave it blank and trading halts at once.
  A halt cancels every resting exchange order and every OTC offer still waiting for an answer, and
  hands back what they had set aside, as the original does. Halting is how a year ends: the year has
  to be halted before it can be closed, so the year end surrenders and banks on everything a company
  holds.
- **Close the year** — runs the year-end reconciliation (offsets first up to the limit, then
  allowances by vintage), banks what may be banked, forfeits the rest, applies penalties, and saves
  the run.
- **End the exercise** — available once the last year has been closed.
- **Player messaging** — the on/off switch. Turn it off during a debrief so the room stops typing.
- **Fine a unit** — a cash fine with a reason, for a rule breach you want the room to see.
- **Hand out offsets** — gives a company a volume of offsets on top of the scenario's own supply.
  The shipped scenario hands every unit offsets with its free allocation each year (2% of its
  emissions, and 12% to one company in ten, which then has offsets to sell).
- **Add abatement for a unit** — implements a project on a team's behalf, for a teachable moment.
- **End-of-year modification** — writes a change in emissions (negative to cut) into one unit's
  business-as-usual path for a chosen year, so a shock survives a save and reload. It cannot be
  undone or listed separately, so keep a note of what you applied.
- **Automatic trading** — one switch per unit. Turning a human unit on lets the AI run it; turning
  an AI unit off makes it passive. The switch rebuilds the AI fleet, so a bot's progress is dropped
  when you use it.

- **Change the rules** — moves the price collar while the year is running: put the new floor and/or
  ceiling in and apply. The collar bounds the auctions only, as in the original: auction bids the new
  collar cannot hold come off with their money back, while the exchange keeps trading in its own
  band around its own last price, so it moves towards a new floor trade by trade rather than jumping.
  The bots re-value a tonne on the spot and re-quote on their next pass. The room is told rather than
  surprised: the change lands in each unit's **Messages**, and the console shows the last five changes
  with who made them and how many bids the new collar released. Only the collar can move mid-run - the cap, the free allocation
  split, the growth path and the auction lots are all fixed when the exercise is built.

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
- **Leaderboard** — companies in compliance first (a short company cannot win, however cheap), then
  by marginal cost of compliance, so the cheapest compliers are at the top. Each unit is named with
  the player who registered for it. Negative outliers are teams that were paid to abate.
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

- **A run that is under way lives in memory.** The host saves it when the exercise is created and at
  each year end, so a crash loses the current year and nothing before it; that year is played again
  after the reload. The saved copy appears under **Saved
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

The clone is behaviourally faithful within the envelopes in the bot-fidelity brief where they hold,
not numerically identical to the original; reports/2026-10-21-fidelity-match.md checks every rule
and screen against the sources. On the seeded three-year run of the shipped scenario (2026-10-22) the
auction averages 137.90, 236.47 and 268.66 (the original's own demo year 1 averaged 123.55), later
vintages price above earlier ones, abatement takes 0.71% of the cap off year one, offsets surrendered
are 2.99% of the cap, offsets trade at a 12.8% discount to same-year allowances on the exchange, and a
mid-run collar shock moves the market inside one virtual month. Three envelopes are open and recorded
as skipped measurements rather than tuned away: **every bot complies** (485 of 726 company-years do),
**year one clears at the floor** (137.90 against 100-110) and the **leaderboard spread** (up to
122.04 per tonne, from year-3 penalties). Whether to hold the brief's bands or the original's observed
figures is an open decision. Treat the price path and the leaderboard as directionally right, and do
not present a specific number as the original's.

These figures are the ones in tests/CarbonSim.Engine.Tests/Golden/fidelity-golden.md, which the
suite asserts on every run: if a change moves the seeded run, that file changes and the build says
so. Read the numbers there rather than trusting this paragraph.

