# QA feedback 06 (from Claude). Round 5 verified live: Change the rules works (floor 100 -> 140 shown to the
trainer, player's Rules/AUCTION FLOOR PRICE shows 140.00, best bid re-quoted 96.00 -> 134.40 within seconds).

## M1 (bug) The shock leaves a crossed book
Steps: run at ~2.5 min, book is bid 96.00 / offer 104.00 (bots only). Trainer raises the floor to 140.
Result: Best bid 134.40, Best offer 104.00, for 60+ seconds. The book is crossed (bid above offer) and
nothing trades. Orders resting below the new floor (the 104 asks) must be re-evaluated when the collar
moves: cancel and release escrow (with a notice to the owner) or re-price, and a bid above a resting
ask must match. Expected: after a floor raise no ask below the floor survives, and the matcher runs on
the re-anchored book so bid >= ask can never stand. Same for a ceiling cut (bids above the ceiling).
Engine red test with the fake clock; also a Playwright step as the trainer-and-player walk.

## M2 Shock is silent to players
Messages stays (0) after the rule change. Broadcast a system message ("Floor price is now 140.00 USD,
ceiling 300.00 USD") to all players of the run and raise the same ParametersChanged push so open
screens refresh. Also record the change in the admin console (who/when/old/new) so the trainer can
re-announce it.

## M3 Change the rules scope (decision, not a defect)
Only the collar can be changed. The brief's shock envelope names "cap cut, floor raise". Add the
annual cap reduction and the cap for future years (not the year in progress) to the same card, or tell me
why that is unsafe mid-run. Floor/ceiling is enough for the demo agenda if you prefer to keep scope small.

## M4 Close the compliance / price-path gap by fixing the abatement menu, not the offsets
Your diagnosis is the useful part: "the mid-priced menu tier costs about 190 a tonne while expected
prices in years 1-2 are 100-115, so market-price abatement cannot fire before year 3". That is a
property of the menu we authored (original fiction), not of the rules. The documented world has
options across the whole range: the video shows an option costing about $0.03/t, and the Mexico/Vietnam
decks have MACC curves with steady steps up to ~300. Please re-author the sector abatement menus
(scenarios/vietnam-2024.json, notes updated, original fiction) so there are several options spread
between ~5 and ~250 $/t over the service life, in particular a tier around 110-160 $/t, and no more
than about 1% of cap economic at the year-1 floor price 100. Then re-run the grid with the shipped
bot settings (offsets 3% + 25% every 5th, margin 1.05) and the earlier offset points. Target: year 1
zero/near-zero penalised units and abatement about 1% of cap; years 2-3 compliance >= 74%; year-3 auction
average 160-260; exchange tracks auction; offsets discount 5-25%; bot cost -60 to +40. If a point exists,
ship it and un-skip the compliance envelope. If not, show the grid and stop; I will treat that as a
documented limit and move on.

## M5 Thin depth (small)
All bots quote the same two prices (96.00 and 104.00 with millions of tonnes at each). Real books
have a ladder. Give bot quotes per-unit jitter within the volatility band so the top-5 levels
differ, and keep it deterministic (seeded).

Red/green, no commits, keep :5003 serving, report back with the usual command. Order: M1, M2, M5, M4, M3.
