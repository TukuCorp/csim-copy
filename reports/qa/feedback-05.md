# QA feedback 05 (from Claude)

K1-K4 accepted as reported (I will verify K1 and K3 live). Thank you for stating plainly what is
not delivered. I do not yet accept "compliance and the rising price path are mutually exclusive".
Your frontier varied one lever (offsets) and pushed it to the 10% limit. Evidence that the lever set is
incomplete:

## L1 Sweep the other levers before declaring a frontier
Documented outcomes that coexist with the deck's parameters (90% free, 10% auctioned, 3%/yr, 2-6%):
 - Mexico report: compliance 74-84% AND average allowance price 160-219, average offset 173-213.
 - Demo video year 1: zero penalties, 46.6 Mt sold at auction (more than the 35.6 Mt nominal 10%),
   9.29 Mt offsets surrendered, 2.95 Mt abated, average allowance 123.55.
Our shipped run: year-3 auction average 242.70 with 197 of 242 units penalised. A price at 243 says bots
value allowances at 243, yet the MACC options they hold cost 5-220 per tonne (my unit: P1 ~5, P2 ~160,
P3 ~220 $/t, 21% of baseline together). Rational bots should abate rather than pay 243 or take a penalty of
300 + an allowance. Abatement year 1 = 1.00% of cap and I see nothing about years 2-3.
Please report, per year, for the shipped run: abatement implemented (count, tonnes, share of cap),
share of units that used each MACC option, how many units were capital-constrained, and how much the
abatement rule's time horizon (implementation years, "don't wait to abate" from the lecture) delays
benefits. Then sweep these together, not one at a time:
  (a) bot abatement aggressiveness / lookahead (adopt any option with $/t below the expected price
      of the years it will operate, subject to build time), (b) Government Reserve To Auction (%) and
      forward-vintage supply (video: auction sales above the nominal 10%), (c) offsets disbursed,
      (d) banking behaviour (bots should bank surplus early and use it).
Choose a point where: year 1 about zero penalised units, years 2-3 compliance >= 74% of units, year-3 auction
average inside roughly 160-260 (Mexico band up to just above), exchange within the J2 band of auction,
offset discount 5-25%. If after sweeping all four there is truly no such point, show the grid
(compliance x price) so I can judge; otherwise ship the point and un-skip the compliance envelope.

## L2 Regulator shock via the admin console (needed for the training agenda)
Definition of done: "pause and shock the market". Today "Rules of the exercise" on the run console is read-only and
"End-of-year modification" only changes a unit's emissions. Add an admin "Change rules" action (cap, annual
reduction, floor, ceiling, penalty, offset limit) usable while Running or Paused, applying to the current and
future years, that: broadcasts ParametersChanged and a message to players, re-anchors the exchange
volatility band (a floor raise must lift the band, orders outside it are re-evaluated), and makes bots
re-price on the next tick (not the next auction). Then un-skip the shock envelope: a floor raise or cap cut must
move exchange prices within one virtual month (about 1m40s at 20-minute years). Test engine-level with the
fake clock and add a Playwright step: trainer raises the floor, a player sees the Rules gear/messages update and the
price move.

## L3 Housekeeping
- If anything is changed in the scenario, update the scenario notes and the runbook figures (docs/trainer-runbook.md
  quotes a price path) so they match what a trainer will actually see.
- Report back with the compact table again (Normal, Hard), plus the per-year abatement table above.

Red/green, no commits, keep :5003 serving, report back with the usual command. If L1 and L2 together are
too large, finish L2 first (shipping a working shock control matters more for training than 20 points of
compliance), then L1.
