# Brief players: play two companies through three years and test the player screens

Folder: `reports/playtest/`. Output file: `reports/playtest/playtest-players.md`. Throwaway files go in
`reports/playtest/_playtest/`. Read `reports/playtest/playtest-common.md` first; its accounts, quirks,
reporting format and hard rules apply.

Context: you play two human companies, **Alpha** and **Bravo**, each in its own browser session
(`alpha`, `bravo`). Another agent (`trainer`) runs the clock and the console; you do not touch the
console. Two players at once is exactly what the real teammate session will be, so test the
interactions between them.

## Task
1. Register both players on the sign-in page (accounts in the common notes). Pick two different
   companies, ideally one from the power sector and one cement or steel company. Then write the file
   `reports/playtest/_playtest/players-ready`. Wait for `_playtest/year-1-started` (check every 20 s).
2. Each year, with both companies, work through as many of these as the year allows (6 minutes, four
   auctions, each window open at the start of its section):
   - Read the top bar (cash, position, best bid/offer, last trade) before and after each action, and
     check the arithmetic: an auction win of V tonnes at clearing price P should cost V x P and move the
     position by V.
   - Bid in an auction while it is open (e.g. 50,000 t at the ceiling price); try a bid while it is
     closed and record the refusal text. After clearing, find what you won and paid.
   - Exchange: place a limit order that rests, see it in "My orders", cancel it; place an order that
     fills against the book and find the trade in your history.
   - OTC: Alpha offers some allowances to Bravo's company; Bravo sees it, accepts one offer and rejects
     another. Check both holdings and cash moved correctly.
   - Abatement: read the cost curve, implement the cheapest project for one company, check cash and
     forecast emissions changed and the project shows its status.
   - Surrender and banking: read the screen before year end; after the trainer closes the year, check
     what was surrendered, any penalty, and what was banked.
   - Glance at System info, Leaderboard and Messages; switch one session to Tiếng Việt once and look
     for untranslated text.
3. Watch for freezes: if a screen stops updating or shows a reconnect banner, note the time and screen.
4. Keep going for years 2 and 3 when `_playtest/year-2-started` / `year-3-started` appear. If the
   trainer lane stalls (no new year file for 10 minutes after a year should have ended), finish with
   what you have.

## Output
`reports/playtest/playtest-players.md`: Summary (companies played, years played, actions tried, how
many succeeded); Per-year log for each company (position, cash, auctions won, trades, OTC, abatement,
surrender result); Findings table (severity, screen, steps, expected, actual, evidence,
OBSERVED/SUSPECTED); Arithmetic checks (each with the numbers); What worked well (5 bullets); What
would confuse a first-time player.

## Last action (mandatory)
Stop both browser sessions (`CHROME_DEVTOOLS_AXI_SESSION=alpha chrome-devtools-axi stop`, same for
`bravo`), then as your very last action run:
herdr agent prompt w38:p1 "players finished: N findings, years played Y, file reports/playtest/playtest-players.md"
(replace N and Y with the real numbers; do not use --wait). If you could not finish, still run it with the
true state, e.g. "players finished: partial, ...".
