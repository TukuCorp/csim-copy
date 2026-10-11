# Handover for a fresh Codex session (from Claude, orchestrating)

You are continuing an architecture-deepening pass on csim-copy (CarbonSim clone, .NET 9, Engine/Data/Web).
Read first: CLAUDE.md, CONTEXT.md (glossary), reports/arch/architecture-review-2026-10-11.html (the 7
candidates; candidates 1, 2, 3 are done), reports/arch/refactor-01.md ... refactor-05.md (the briefs so far),
the "QA loop" and "Architecture pass" sections at the end of activeContext.md, lessons.md.

## Rules (unchanged)
- Red/green TDD. NO commits. Keep tests green; the fidelity golden (tests/CarbonSim.Engine.Tests/Golden/
  fidelity-golden.md, both bot-log hashes) must stay byte-identical unless a task says otherwise.
- LF line endings, .editorconfig, no python text rewrites of source files (use your editor/patch tools).
- BEFORE building the solution stop the process serving :5003 (it locks CarbonSim.Web DLLs), AFTER finishing
  restart it (dotnet run --project src/CarbonSim.Web --launch-profile http). Test projects build without that.
- Build after EVERY file edit; never leave a non-compiling or red tree. Check a claim before you report it:
  do not report "green" without having run the suite after your last edit, and do not report "bites"
  without seeing the red.
- Report: write text to reports/arch/codex-report.txt (starts with a letter), then run
  herdr agent prompt w2C:p2 (Get-Content reports/arch/codex-report.txt -Raw)
  Do not wait for a reply. One task per prompt; each prompt will name the task.

## Known state
- Engine: SimulationRun (Engine/Run), Rules accessor (Simulation.Rules), Standing (Engine/Allocation) are in.
  Scoring, ComplianceRegister, SystemReport, ComplianceBot, PlayerSession, AdminSession ask Standing.
  Production code no longer uses CompliancePosition; only tests/harnesses do (TrainingRun 2, AbatementPortfolioTests 2,
  FidelityRun 3, ReportingTests 2, SnapshotRoundTripTests 3, ConsoleDoorTests 1, CompliancePositionTests 9).
- RED right now (the previous session's own report said so): tests/CarbonSim.Web.Tests/Simulations/
  RestingOrderPositionTests.cs (A_resting_sell_order_still_counts_as_held) FAILS; Web suite 1 failed, 184 passed, 1 skipped.
  Engine 285 + 2 skipped and Data 16 are green.

## Task queue (do them in this order, one per prompt)
T1 Fix the red test. Read the assertion text. Either the test setup does not escrow (fix the setup) or the
   player view's Held still ignores escrow (fix the view to use Standing.Held). Then PROVE it bites: temporarily make
   Standing.Held ignore escrow, SEE the test fail, revert, see it pass. Report the two outputs.
T2 Candidate 4 (band): OrderBook.FitToBand(price): clamp to [BandFloor, BandCeiling], round to 2 dp, and if the
   rounded value is below BandFloor use ceil(BandFloor*100)/100, above BandCeiling floor(BandCeiling*100)/100, so
   Validate always accepts it. ComplianceBot.Buy/Sell (Clamp with book band, ~lines 544/574) call it. Fix the
   wrong comment on the bot's catch (the band cannot move between clamp and submit under the run gate; the real
   cause was rounding past the band edge). PlayerSession.cs ~558-559 must use the exchange's own band, not
   re-implement floor*(1+-band). Deterministic edge-case Engine test (red first). If the golden moves, list how
   many bot actions changed, confirm envelopes in force hold, re-baseline once with CARBONSIM_UPDATE_GOLDEN=1.
T3 Candidate 5a: delete the duplicate ToTotals(SystemTotals) copies in PlayerSession and AdminSession and the
   SystemTotalsView mirror: one run-wide read model built once.
T4 Candidate 5b: PlayerSession.PlaceOrderAsync returns the order outcome (id, fills, resting/refused) instead of
   Task; Exchange.razor (~282-305) uses it instead of inferring "the newest of my orders" from the next view.
T5 Delete CompliancePosition: move the remaining test/harness users onto Standing (mind the sign: Position is
   long-positive, the old PositionFor was short-positive), delete CompliancePositionTests if StandingTests covers it
   (list what covers each deleted test).
