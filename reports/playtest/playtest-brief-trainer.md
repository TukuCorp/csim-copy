# Brief trainer: run the exercise clock and test the trainer console

Folder: `reports/playtest/`. Output file: `reports/playtest/playtest-trainer.md`. Throwaway files go in
`reports/playtest/_playtest/`. Read `reports/playtest/playtest-common.md` first; its accounts, quirks,
reporting format and hard rules apply.

Context: you are the trainer. Another agent (`players`) plays two companies (Alpha and Bravo) and tests
the player screens; you own the clock and the console. The players depend on you to start each year.

## Task
1. Sign in as the trainer (session `trainer`) and open the Trainer console. Create a new exercise:
   keep the defaults except **YEAR LENGTH (MIN) = 6** and **AUCTION WINDOW (MIN) = 0.75**, 3 years.
   Before you create it, wait until the file `reports/playtest/_playtest/players-ready` exists (the
   players lane writes it after registering both companies); check every 20 s, give up waiting after 10
   minutes and carry on. Write `reports/playtest/_playtest/exercise-created` once the exercise exists.
2. Start year 1. Write `reports/playtest/_playtest/year-1-started`. While the year runs, visit every
   console page you can find (run view, reports, surrender status by company, leaderboard or system
   report, messages) and check that numbers update. Try one shock control if offered (for example
   pause and resume the clock once, or halt and reopen trading) and note what players should see.
3. When the year's time is up, close the year the way the console offers (halt trading / close the
   year). Note whether the console makes the next step obvious. Start year 2 (write
   `year-2-started`), then repeat for year 3 (`year-3-started`). After year 3, end the game if the
   console offers it, and read the final leaderboard and system report.
4. Throughout, test what the lessons say must hold: a year must not start by itself; a year whose
   time is over must not be resumable (the console should say why); a control should not be offered in
   a state where it cannot act.
5. Record a timeline: clock time, year, auction number, action taken, what the screen showed.

## Output
`reports/playtest/playtest-trainer.md`: Summary (did 3 years complete? yes/no, total time); Timeline;
Findings table (severity, screen, steps, expected, actual, evidence, OBSERVED/SUSPECTED); Final
leaderboard top 5 and where Alpha and Bravo ranked; What worked well (5 bullets); Open questions.

## Last action (mandatory)
Stop your browser session (`CHROME_DEVTOOLS_AXI_SESSION=trainer chrome-devtools-axi stop`), then as
your very last action run:
herdr agent prompt w38:p1 "trainer finished: N findings, years completed Y, file reports/playtest/playtest-trainer.md"
(replace N and Y with the real numbers; do not use --wait). If you could not finish, still run it with the
true state, e.g. "trainer finished: partial, ...".
