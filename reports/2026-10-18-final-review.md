# Final review - 2026-10-18

Three read-only reviewers covered the engine, the host and persistence, and the UI and tests. I
checked every finding against the code before acting on it. The fixes below each have a test that
failed first. The open items are the ones that move the seeded golden, need a design decision, or
are hardening the internal demo does not depend on.

Gate after the fixes: build 0 warnings, `dotnet format` clean, Engine 294 passed + 2 skipped, Data
18, Web 214 + 1 skipped (526 passed, 3 skipped, 0 failed). The fidelity golden is byte-identical.

## Fixed

| Area | Defect | Fix | Test |
|---|---|---|---|
| Exchange | A market or stop buy walking a thin book could reach offers above the cash it set aside; the settle then threw after the allowances had moved | Unpriced buys are capped at the price they escrowed (`OrderBook.SpendCap`) | `ExchangeTests.A_market_buy_never_pays_more_a_tonne_than_it_set_aside` |
| Exchange | A stop buy that filled completely kept its unspent escrow, shrinking the company's cash for good | Release unused cash after every stop fill | `ExchangeTests.A_stop_buy_that_fills_completely_hands_back_the_cash_it_did_not_spend` |
| Exchange | Any collar change, even ceiling-only, reset every book to the floor and cancelled orders outside that band | Re-anchor on the last trade clamped into the new collar | `SimulationRunTests.Moving_only_the_ceiling_leaves_the_market_where_it_was_trading` |
| Exchange | "Orders released" counted every collar change so far, not the latest | Reset the count at the start of each change | `SimulationRunTests.A_collar_change_counts_only_the_orders_it_released` |
| Run | A year halted early never cleared its later auctions, so their lots never returned to the reserve | `EndYear` clears every auction of the year that has not run | `SimulationRunTests.Closing_the_year_early_clears_every_auction_left_in_it` |
| Rules | A floor of 0 passed validation and the collar change, then broke the exchange band and the bots' price division | Floor must be greater than zero, checked before anything moves | `ParametersTests` row, `SimulationRunTests.A_floor_of_zero_is_refused_before_anything_moves` |
| Auction | Bid ids were the bid count, so a released bid made the next id repeat one already issued | Next id follows the highest issued | `SimulationRunTests.A_bid_placed_after_a_release_gets_an_id_of_its_own` |
| Clock | A warned halt survived an immediate halt and halted again on resume | The immediate halt clears the pending one | `SimulationClockTests.An_immediate_halt_replaces_a_warned_one_so_resuming_does_not_halt_again` |
| Console | Load on a live exercise replaced it with its last save, losing everything played since | Driver refuses; the button is hidden for live runs | `AdminRunTests.Loading_a_run_that_is_already_live_is_refused_and_keeps_its_progress`, `AdminScreensTests` |
| Accounts | Concurrent wrong reset codes lost increments (4-7 of 16 counted), so the attempt cap could be beaten by a burst | A try is claimed with one conditional `UPDATE` before the code is checked | `AccountStoreTests.Reset_attempts_racing_each_other_cannot_share_a_try` |
| Accounts | Two registrations racing for one company gave one of them a raw 500 | A unique-index refusal returns the same "taken" answer as the checks | `AccountStoreTests.Claims_racing_for_one_company_give_one_account_and_refuse_the_rest` |
| Host | A failed save after a change (the year end) threw away the broadcast, so screens kept the old state | Broadcast first, then report the save failure | `SaveFailureBroadcastTests` |
| Screens | Every refusal and status notice was wiped by the next one-second refresh | Refresh keeps the notice until it is dismissed or another control replaces it | `FeedbackScreensTests.A_notice_stays_through_the_refresh_until_it_is_dismissed` |
| Screens | One failed read stopped a screen's refresh loop for good, freezing its countdowns | The loop logs and carries on | `LayoutRefreshTests` |
| Screens | The front door loaded once, so a waiting room never saw the exercise appear | It re-reads on the shell's refresh interval | `WaitingRefreshTests` |
| Charts | Charts were never disposed and left a window resize handler per page visit; they also redrew every second | Dispose with the page, remove the handler, draw only a changed option | `ChartLifetimeTests` |
| Tests | Broadcast tests wrote event lists from SignalR threads while reading them, and one negative check waited a fixed 200 ms | `ConcurrentQueue`, wait on both connections, and wait for a later pause before asserting nothing else arrived | `AuctionBroadcastTests`, `BotFillBroadcastTests` |

Also tidied: dead code removed (`OrderBook.ReferencePrice`, a one-argument `IsWithinBand`, an unused
array in the bot's auction bid, `CompanyFinance.AvailableCapital` whose comment was wrong), the
double-logged tick failure, 16 unused resource keys in both languages, a stale skip reason on the
compliance envelope, and the CI actions moved to their Node 24 majors (checkout v7, setup-dotnet v6,
upload-artifact v7).

## Open - moves the golden, needs a decision

1. **Escrow at year end (high).** `ComplianceRegister` surrenders and banks from available
   allowances only, while `Standing` (the screens) counts escrowed ones as held. A company with
   allowances in a resting sell order reads as covered and is then penalised. Surplus parked in an
   order at year end is neither banked nor forfeited, which also gets around the banking cap. The fix
   is to cancel resting orders and pending OTC offers before reconciling. Long bots rest surplus sells
   at year end, so this moves the golden and needs a deliberate re-baseline.
2. **Bot abatement horizon (medium).** `ComplianceBot` buys projects in the last year that only start
   operating after the run ends (`ImplementationYears > remainingYears` should be `>=`). Moves the
   golden.
3. **Bot double-buys offsets (low).** The short path of the bot's settle places a second offset buy
   without counting the one already resting. Moves the golden.

These three are best done together in one calibration round, with the envelopes re-measured.

## Open - hardening the internal demo can live without

- **No rate limiting** on sign-in, registration or the reset routes. The atomic claim closes the
  burst bypass, but sequential guessing is limited only by the five tries per code. Add
  `AddRateLimiter` per IP and per address before anything faces the public internet.
- **A password reset does not end other sessions**, and a role change does not reach a live cookie.
  Needs a security stamp checked in `OnValidatePrincipal`.
- **The hub sends OTC offers to the whole room**, and `JoinSimulation` only checks sign-in. The player
  screens show only your own offers, but a scripted client could see every negotiation. Send offers
  to the two parties and check the caller plays in the run.
- **Response timing reveals registered addresses** on sign-in and reset request (no hash for an
  unknown address).
- **Sectors come back sorted by name after a save and load** (no ordinal column). Needs a migration.
- **English text in the Vietnamese UI**: raw enum names on the OTC, exchange and dashboard tables,
  and engine exception messages shown as they are.
- **Exchange: the book viewer and the order form share one product field.** Choosing a book also
  picks the order's product. It is visible on screen, so I left it as a design question rather than a
  bug.
- **Chat history is unbounded in memory** and has no length limit.
