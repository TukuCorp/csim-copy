# Architecture pass, round A (from Claude): safety net, then two verified defects

Context: reports/arch/architecture-review-2026-10-11.html (open it for diagrams). I reviewed it on the
user's behalf and chose: candidate 1 (run module, with the rules accessor from 3 folded in), then 2, 4, 5.
Not doing 6 (bot split, seeded draw order risk) or 7 (persistence). Vocabulary: module, interface, depth,
seam, locality, leverage; please use it in notes. A snapshot of the working tree before this pass is
kept in my scratchpad, so work freely, but: NO commits, keep :5003 serving at the end of each round,
keep every existing test green, never loosen a fidelity envelope to make a refactor pass.

## A0 Baseline first (before touching anything)
1. Run the whole suite and record counts (expect Engine 259 + 2 skip, Data 16, Web 179 + 1 skip).
2. Capture a GOLDEN of the calibrated run: the FidelityMeasurements table for Normal and Hard
   (per-year auction average, exchange average, fills, penalised units, compliant company-years,
   offsets, abatement share, bot MCC range) plus a hash of the BotAction log for the seeded 242-unit run.
   Check it in under tests/CarbonSim.Engine.Tests/Golden/ with a test that fails if it changes. Every later
   round must keep this golden identical unless a round explicitly says the numbers move (A2 below does,
   and re-baselines it deliberately, once).

## A1 (defect, verified by me) The host never plays the run the envelopes measure
AuctionSchedule.Build(simulation) defaults forwardVintageShare to 0. The host calls it that way
(src/CarbonSim.Web/Simulations/SimulationRegistry.cs ~336, and SimulationBots.cs:83 defaults too). Every test
harness (FidelityRun, TrainingRun, SnapshotRoundTripTests, AuctionTests, AllowanceConservationTests)
passes 0.20. So the live auction offers only the current vintage ("Vintages this auction: Vintage 1")
while the calibration assumes forward vintages; the video script (frame 7: vintages this auction, per-vintage
volume) and the plan (forward vintages) expect them live.
Decision: the played run uses 0.20, as calibrated. Make it a single named setting that the host,
SimulationBots and every harness read from the same place (the scenario/run options, default 0.20,
documented in the scenario notes), not a default argument that tests override. Red test first: a host-built
run offers a forward vintage in auction 1 and its schedule equals the one the Fidelity harness builds.
Then run the live app: auction 1 must list more than one vintage, bidding on a forward vintage must work,
and the player auction page, exchange products and System info reserve table must stay coherent.
Re-measure the envelopes with the host wiring. If numbers move, report old -> new, and re-baseline the
golden once, deliberately, with the reason in the commit-less notes.

## A2 (defect, verified by me) Nothing refuses trading outside its window
SimulationRegistry.BidAtAuctionAsync (~417) and PlaceOrderAsync (~432) check ownership only;
Auction.PlaceBid checks IsCleared only. The disabled Place bid button is the only guard, so a hub client
(or a stale page) can bid when the auction window is closed, and place or accept orders while trading is
halted, paused or the year has ended. Decide and implement (my decisions):
 - Auction bids: only while that section's auction window is open and the run is Running.
 - Exchange place order, OTC send offer, OTC accept: only while the run is Running (not Paused,
   TradingHalted, YearEnded, GameEnded, Pending). Cancelling own orders is always allowed.
 - Abatement, surrender view, chat, admin actions: unchanged.
 - Bots obey the same rule (they should never hit the refusal in a normal run; if they do, that is a bug to report).
 - Refusals are typed errors the UI shows beside the action ("Trading is halted", "The auction is closed"),
   in en and vi; the player Exchange/Auction pages already show a result line.
Red tests first at the Engine level with the fake clock (not only through the hub): bid after the window,
order while halted, order after year end, cancel while halted succeeds. Add one Playwright step:
trainer halts trading, player presses Place order, sees the refusal and no order appears.

## Report back
Per item what changed, test counts, the golden before/after, and any surprise. If A1 shifts the
calibration materially (compliance, price path, year-1 penalties), stop, say so with the numbers and do not
paper over it.
