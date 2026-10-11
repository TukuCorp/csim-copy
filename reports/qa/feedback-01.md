# QA feedback 01 (from Claude, browser testing of http://localhost:5003)

Context: I am testing the running app against `activeContext.md` (Definition of done) and
`research/2026-09-13_carbonsim-video-insights.md` (acceptance script). Admin login worked.

## F1 (blocker) The seeded run never starts: clock says Running but nothing advances
Repro: sign in as admin@carbonsim.local -> /admin -> Open the Vietnam ETS Pilot 2024 run -> press
"Let the clock run". State flips to Running, but YEAR stays 1/3, AUCTION shows 4 / 4, and TIME LEFT IN
THE YEAR, NEXT AUCTION OPENS IN and AUCTION CLOSES IN all stay 0m 00s, even after reloading the page
and waiting 10+ seconds. A freshly seeded, never-started run should show Pending (or "Year 1 not begun"),
Auction 1 / 4, and a full 20:00 year; pressing the start control (the original calls it "Begin Year")
should begin year 1 and the countdowns should tick. Also check there is a distinct "Begin Year" action
for each new year (video script: "Begin Year is pressed by the admin" after the year-end report).
Expected per the plan: 3 years x 20 min, 4 auctions of ~2m15s each, open 45% of the year.

## F2 Pre-start state is mislabelled
Before the clock is started the admin page and the player waiting screen show "Trading halted - Year 1".
It should read as not started (Pending / Waiting), not as a trading halt.

## F3 Duplicate names in the "Fine a unit" selector
Every option reads "Công ty Điện lực Bình Minh (Công ty Điện lực Bình Minh)" and "AI Power 01 (AI Power 01)".
Show unit name once, or "Company - Unit" only when they differ; mark bots "(AI)" as the leaderboard does.

## F4 Signed-in state is not reflected on the public waiting screen
After signing in as the trainer you land on "/" and it still shows the "Sign in" link. When signed in it
should show who you are and a path onward (trainer -> /admin console, player -> their run). /admin
when signed out shows "Only a trainer account may use this console." with no link to /sign-in.

## F5 Waiting screen counts
COMPANIES 242, UNITS 242, PEOPLE 37. Fine if intentional, but the briefing says 242 enterprises / ~38 human;
please confirm the labels mean what a trainer expects (human companies vs AI companies).

## How to work
- Follow CLAUDE.md: red/green, failing test first, engine tests deterministic, no commits unless asked.
- Fix F1 first (root cause, not a patch). F2-F5 are small.
- Keep the app serving on http://localhost:5003 (restart it after your fixes) so I can retest.
