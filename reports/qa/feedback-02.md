# QA feedback 02 (from Claude). Round 1 (F1-F5) verified fixed: run starts, clock ticks, labels, signed-in header.

Tested live on http://localhost:5003 as admin and as a registered player (Công ty Điện lực Bình Minh),
English and Vietnamese, desktop 1440x900. Screenshots are in reports/qa/*.png (dash, abatement,
exchange, auction, auction2, vi-dash2). Priorities: G1-G3 are correctness, the rest are fidelity/polish.

## G1 (correctness) Position ignores allowances bought at auction / held
Repro: place a bid for 256,000 t in auction 1; it clears (capital fell by 25,600,000, holdings show
Vintage 1 = 2,551,233 = 2,295,233 free + 256,000 bought). But top bar, Dashboard "My long / short position",
Surrender and banking, and the admin Company report still say "Short 255,025" with the same
free allocation bar. Expected: current/forecast position = allowances held (+ offsets within the limit)
minus forecast emissions, so this unit should read Long 975 and the bar should show bought allowances.
The admin Company report SHORTFALL column has the same flaw. Also the leaderboard "FINAL POSITION"
column shows 2,295,233 (= allowances held) for every unit; the video spec calls it Final Long/Short
Position, i.e. held minus emissions with a Long/Short label. Fix in the engine (red test first), then
every screen that reads it.

## G2 (correctness/fidelity) Auction outcome is not shown to the player
After the auction closed the player got no results modal ("Auction has ended" listing per-unit bid
volume, won, paid, cost - video frame/section 7) and the "My auction history" table has no
won / paid columns: AWARDED (6,203,902) is the system-wide total, YOU BID is my 256,000, and nothing
says I won 256,000 at 100.00 (I inferred it from capital). Add: results modal on close, and per-row
columns My won / My paid. Also show a confirmation after Place bid (today nothing visibly confirms it;
the only trace is the history row's YOU BID).

## G3 (verify) Do the bots act on the exchange at all?
Four minutes into year 1 (two auctions cleared, bots took ~5.9M of the 8.9M offered in auction 1) the
Exchange book for every vintage is empty: no bot bids/offers, no trades, Last trade is just the floor
anchor. The video shows market orders filling against bot depth and limit orders filling below limit.
Please run the seeded 242-unit run live (or a test with the real driver) and confirm bots rest orders
during a year; if they do not, find out why (trigger times, orders refused, bots only reacting at
year end?). Fidelity envelopes in research/2026-09-13_bot-logic-and-numeric-fidelity-options.md section 6
apply. Report the measured exchange activity per year.

## G4 Language switch is broken and invisible
- Links are "/culture/set?culture=vi&amp;amp;redirect=/run/..."; the double-encoded "&amp;amp;" makes the
  redirect param unreadable, so switching language always lands on "/" (waiting screen) instead of the
  page you were on. Fix the encoding.
- On run pages the English/Vietnamese links sit in a <p class="small"> under the progress strip, but
  document.elementFromPoint at the link returns div.banner.banner-auction, so the links are covered and
  not visible/clickable for a normal user. The waiting screen and sign-in page have no switch at all.
  Put a visible switch in the top bar of every page (flag/short label EN | VI as in the video).
- Link text is "Tiếng Anh" when the UI is Vietnamese; label each language in its own language
  (English, Tiếng Việt).

## G5 Waiting screen heading is wrong when the run is already Running
"/" says "Waiting for the simulation to start" with status "Running · Year 1". Show that heading only in
the Waiting/Pending state; otherwise show the run status and a clear "Open" call to action. Layout nit:
the "Open" button sits flush against the "Signed in as ..." text (see vi-dash.png).

## G6 MACC chart does not encode tonnes
Abatement page: the three bars are the same narrow width and evenly spaced. The spec (video section 6):
width = tonnes, height = $/t, sorted by cost, hover shows technology. Make bar width proportional to the
option's annual reduction, add a legend/tooltip with project name, tonnes and $/t, and a marker for
the unit's current shortfall so the player can read the cheapest way to close it. The options table
below is wider than its card and gets clipped at 1440px; fix horizontal overflow.

## G7 Exchange price chart and summary strip
- Legend swatches for Vintage 1/2/3/Offset are the same green, so series cannot be told apart.
  Use distinct colours/dash styles (also for colour-blind readers).
- Summary strip label reads "ALLOWANCE AUCTION AUCTION FLOOR PRICE" (duplicated word).
- Video section 8 expects the strip to show Exchange best bid / last trade / best offer, OTC best price,
  and auction low / cleared / high / average. Add the Exchange and OTC groups.
- Order form has one "Immediate or cancel" checkbox; the spec shows Partial Fill and IOC. Confirm
  semantics and label both, with a tooltip.
- "Top of book" shows the bid but has no Selling / Buying headers; add them.

## G8 Unit names still duplicated in lists (F3 not applied everywhere)
- OTC "Counterparty unit": "AI Cement 01 - AI Cement 01" x 240 in a flat unsorted <select>. Show the name
  once, tag bots "(AI)", sort, and make it searchable (or group by sector).
- Leaderboard "UNIT (PLAYER)": "Công ty Điện lực Hồng Hà (Công ty Điện lực Hồng Hà)" - same duplication;
  spec also asks for bots to be labelled "(AI)".
- Leaderboard is all +0.00 / tied right after auction 1 and my company (non-zero) is not near rank 1:
  check ordering (should be by overall marginal cost of compliance, lower is better) and that a unit
  with a non-zero cost ranks consistently. Show my own row highlighted.

## G9 Small things
- Every page's <h1> shows a heavy blue focus outline after navigation (programmatic focus on h1).
  Keep focus management for screen readers but use :focus-visible only for keyboard-initiated focus.
- Console: "A form field element should have an id or name attribute (count: 8)" on player pages.
- Waiting screen/dash: capital 1,275,129,000 USD for a unit emitting 2.5 Mt at 100-300 USD/t looks
  very large (about 50x annual compliance cost at floor price). Check against the trainer-deck
  scenario parameters; if intentional keep it and say so in the scenario notes.

## How to work
- Red/green. Engine fix for G1 first, then G3 investigation (report findings even if the answer is
  "bots are fine, here is the measured activity"), then G2, G4-G9.
- Re-verify live: restart the app on http://localhost:5003 and, if you can, drive the same steps with
  Playwright (bid, check position, language switch keeps the page).
- No commits. Report back per the usual command, with what is fixed, test counts, and anything you
  disagree with. For G3 include the numbers.
