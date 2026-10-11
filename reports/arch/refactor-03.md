# Architecture pass, round C (from Claude): auction-vs-collar bug, test shrink, then Standing (candidate 2)

Round B accepted. Thank you for leaving the auction bug alone and writing the repro. Same rules: red/green,
NO commits, keep :5003 serving, golden byte-identical unless stated, use CONTEXT.md vocabulary.

## C1 Decision on your finding: release auction bids the collar can no longer hold
A bid priced outside the (new) collar when the trainer changes it is released, exactly like exchange orders
the new band cannot hold: floor raised above a resting bid price -> release; ceiling cut below a resting bid
price -> release. Escrow goes back to the company, the owner gets a system message ("Your bid for N t at P
in auction S of year Y was released: the floor is now F"), the collar-change RunResult reports how many auction
bids were released next to the orders released, and the trainer's rule-change line says both counts (en + vi).
Bids inside the collar stay. Red test first: your repro (bid 10 t at 120, floor to 140, section closes) ends
with no exception, the bid released, cash restored, and a notice; plus the ceiling-cut mirror. Also make
Auction.ClearLot / Settle impossible to reach an under-escrowed state by construction: if a bid price is
outside the current collar at clear time that is an invariant violation with a clear message (not an
InsufficientFunds from deep in Settle). Where a lot is undersubscribed, price it as today (at the floor).
Re-run the golden: bots never leave bids outside a changed collar in the seeded run, so it must not move.

## C2 Finish B4: replace, do not layer
ConsoleDoorTests (373 lines) and AdminRunTests: keep only what is wiring (gate, broadcast, ownership,
admin-vs-player doors). Delete assertions that Engine tests now make (year close, collar, refusals), list
what you deleted in the report with the Engine test that now covers it. Keep every Playwright suite.

## C3 Candidate 2: one Standing for a company-year
Read candidate 2 in the HTML review. Today five places answer "where does this company stand":
CompliancePosition.PositionFor/HeldFor, Scoring.Holdings, ComplianceBot.Shortfall/HeldAllowances,
PlayerSession.BuildPosition (~316), AdminSession (~404, ~492), plus Scoring.FinalLongShortPosition with a third rule;
the bot's offset limit is computed four ways; `new CompliancePosition(simulation)` is built at 6 sites;
`Compliance.Results.FirstOrDefault(r => ReferenceEquals(r.Company, c) && r.Year == y)` appears ~10 times.
Deepen into one Engine module (name: Standing, a CONTEXT.md term) with a small interface: for a company
(or unit) and a year, live or settled: emissions the obligation is measured on (the register's formula: rounded,
post-abatement), allowances held, offsets held and usable (capped by the offset limit), position (signed:
long positive), shortfall (>= 0), covered, and whether the year is settled, with the settlement when it is.
My decisions on the definitions (so you do not have to ask):
 - Held counts allowances in escrow (committed to a resting order or bid) because they are still the company's;
   report Available separately where a screen needs it.
 - Vintages usable for the year = the compliance vintage and older. Offsets count up to the year's limit.
 - A settled year answers from its settlement, never from a fresh recomputation (fixes H1-class bugs by construction).
 - The offset limit is computed once, inside Standing; the bot's four variants collapse onto it. If the bot
   uses a deliberately different number (unrounded, ignoring abatement) for pricing, keep it as a bot-local
   estimate but name it as such and test that it only drives price, not compliance decisions.
Callers: Scoring, ComplianceRegister, ComplianceBot, PlayerSession, AdminSession, SystemReport all ask Standing.
ComplianceRegister gains a company-year lookup (one dictionary) so the ~10 linear scans disappear.
The golden must stay byte-identical (the bot and the leaderboard numbers must not move). If a screen's
number changes because it used a different rule before (Available vs Held), list each such screen and the
before/after example in the report: those are bug fixes, say so.
Also look at SystemReport: AllowancesSold and AllowancesBought are built from the identical expression, and so
are OffsetsSold and OffsetsBought. Is that intended (every sale is someone's purchase, so equal by definition)?
If so collapse them to one measured fact or assert the identity with a test; if not, it is a bug, fix and report.
Tests: a table-driven Engine test over (held vintages, escrowed, offsets over/under the limit, settled or not)
that pins Standing; replace scattered position asserts in ReportingTests/ConsoleDoorTests where Standing now
covers them. PlayerViewFixture's hand-set PositionView stays (bUnit renders what it is given) but add one
integration test that a real run's player view position equals Standing for the same company.

## Acceptance (report each)
1. Golden byte-identical (or each deliberate difference listed).
2. Counts of CompliancePosition constructions and Results.FirstOrDefault scans before/after (target 0 outside Standing/register).
3. Lines removed vs added overall in this round.
4. Test counts, build 0 warnings, format clean apart from the CHARSET note.
5. Live: bid, win, check top bar position / Surrender screen / leaderboard final position / admin company report agree;
   raise the floor with a bid resting and see the release notice and the trainer's message.
