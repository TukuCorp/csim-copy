# Architecture pass, round B (from Claude): the run module (candidates 1 + 3)

Round A accepted (golden unchanged, forward vintages live, TradingWindow). Now the main deepening.
Read candidate 1 and 3 in reports/arch/architecture-review-2026-10-11.html. Use the vocabulary (module,
interface, depth, seam, locality, leverage). Rules unchanged: red/green, NO commits, keep :5003 serving,
tests green, golden identical unless this brief says otherwise.

## B0 Name things first: create CONTEXT.md at the repo root
A short domain glossary, in our own words (original wording; research/ is a specification, not content).
Terms to define at least: Simulation, Trading System, Run (one played simulation: Engine module +
the host that drives it), Section (one auction cycle of a year), Trading window, Collar (floor and ceiling),
Rules (current Parameters of a run), Standing (a company's position in a year, candidate 2, to come),
Gate (the host's one-writer rule), Announcement. Name the new modules after these terms.

## B1 One Engine module for a run
Today the order of operations lives in Web (SimulationClockService.TickOnce ~511, EndYearAsync ~171-221,
ChangeRulesAsync, StartAsync, BeginNextYearAsync) and is copied into four test loops
(FidelityRun, TrainingRun, SnapshotRoundTripTests ~581-635, ComplianceBotTests.Run).
Deepen it into one Engine module (suggested name: SimulationRun, namespace CarbonSim.Engine.Run) that
owns, behind a small interface:
 - advance (clock tick -> clear every closed auction -> bots act -> collect notices/events), returning
   one typed result (what cleared, what the bots did, notices raised), so the host broadcasts from a value
   instead of from side channels;
 - start / begin next year / pause / resume / halt (with warning) / end year (halt -> clear the open
   auction FIRST (the K3 fix) -> reconcile -> close year), each returning a typed result;
 - change the collar (floor, ceiling) -> returns the RuleChange record (old/new, orders released) that
   the host announces; this absorbs Parameters mutation, RulesRevision, Exchange.FollowRules and
   Cross, so none of them stay public for callers;
 - the actions a player can take at the market seam (bid, place order, cancel, OTC send/accept/reject), taking an
   engine Unit and typed requests (no actor strings, no ownership: identity and RequireOwner stay in Web).
It takes TimeProvider and the seeded random stream by injection; no threads, no locks (the Gate stays in
Web). Interface = what a trainer-or-test must know; everything else internal. Design it twice
(two radically different shapes) and pick the deeper one; say in your report which two and why.
Then: Web's SimulationClockService and SimulationRegistry call it; the four test loops become
calls into the same module, so the fidelity envelopes measure the run the host plays by construction.

## B2 Collapse the host's gate/collect/broadcast protocol
It is open-coded 5 times in SimulationClockService (Tick, Start, BeginNextYear, EndYear, MutateAsync) and
again in SimulationRegistry (MutateAndAnnounceAsync x2); 8 CollectAnnouncement call sites;
HostedRun.Shared (the screens' run-wide cache) is cleared only inside CollectAnnouncement, so a mutation
path that forgets it leaves stale views. Make ONE primitive that takes the gate, runs a run-module
operation, collects its announcements, invalidates the shared cache, saves when the operation says so, and
broadcasts. Every mutation goes through it; add a test that fails if a mutation path bypasses it
(e.g. every public mutating method on the host is exercised and the shared cache version advances).

## B3 Rules accessor (candidate 3), restricted
~35 sites in 16 files read simulation.TradingSystems.Single().Parameters. Add one accessor (Simulation.Rules
or equivalent) for the current rules, and make Auction, OrderBook, SimulationClock and AuctionSchedule read
through it instead of capturing the Parameters object in constructors. Floor and ceiling setters become
internal to the run module's collar change (public set removed). Do NOT generalise to multiple trading
systems or immutable parameters; only the accessor and the collar owner. The scenario/snapshot/EF field
list for Parameters stays as it is (that is a later problem); but fix the two stale views noticed in the
review: AdminParametersView and SystemInfoView omit GovernmentReserveToAuctionPercent (and now
ForwardVintageShare): add both and show them on System info and the admin console rules card.

## B4 Tests: replace, do not layer
Delete or shrink tests that now duplicate Engine-level cover: ConsoleDoorTests (373 lines) and AdminRunTests
should keep only wiring checks (gate, broadcast, ownership). Add plain Engine tests for: year close
mid-auction clears first, refusals while halted, rule change then cross, advance -> bots -> clear ordering with
the fake clock. Keep every Playwright suite.

## Acceptance (report each)
1. The golden fidelity file is byte-identical. If a loop-vs-host difference makes it move, do NOT
   silently re-baseline: list each difference, say which one is the host's true behaviour, show the envelopes
   before/after; re-baseline only if the host behaviour is the truth and every envelope in force still holds.
2. Line counts before/after for SimulationClockService.cs, SimulationRegistry.cs, FidelityRun.cs, TrainingRun.cs,
   the public member count of the run module, and how many `TradingSystems.Single()` sites remain (target: 0
   outside the accessor).
3. dotnet build 0 warnings; Engine/Data/Web counts; format clean apart from the known CHARSET note.
4. Live: start a run in the app, bid, halt mid-auction, end the year, open the next, change the floor: no
   regressions (same flow I ran in the QA loop).
Report with the usual command. If B1 turns out too large for one round, finish B1+B2 and say what is
left for B3/B4.
