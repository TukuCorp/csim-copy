# Playtest report: players lane (Alpha and Bravo)

Status: **PARTIAL — aborted.** The local CarbonSim server at `http://127.0.0.1:5097` was shut down
for low memory during this lane and did not come back. No player screen was reached, so no play
session data exists.

## Summary

| Item | Value |
|---|---|
| Companies played | None — 0 registered, 0 played |
| Years played | 0 (no year-1-started file ever appeared) |
| Actions tried | 0 of the 8 briefed actions |
| Findings | 0 (no screen was observed, so nothing can be claimed as OBSERVED) |
| Report-back | `players finished: partial, 0 findings, years played 0` |

## Timeline of what actually happened

1. Read `reports/playtest/playtest-brief-players.md` and `reports/playtest/playtest-common.md`.
2. Confirmed `http://127.0.0.1:5097/` answered `200 OK` — the app was up at the start of the lane.
3. Confirmed the tooling was available: `chrome-devtools-axi` (session support present) and `herdr`
   with `HERDR_ENV=1`.
4. Started the `alpha` browser session and issued `chrome-devtools-axi open http://127.0.0.1:5097/`.
   The command returned no page snapshot, and the session never produced a usable snapshot.
5. The server was then shut down for low memory and did not restart. A follow-up probe of
   `http://127.0.0.1:5097/` returned `Unable to connect to the remote server`.
6. The lane was stopped on instruction. Both browser sessions were stopped
   (`alpha` and `bravo`, both reported `status: stopped`).

## Per-year log

None. The trainer lane never created `_playtest/year-1-started` (the `_playtest` directory was empty
when checked), so no year was ever opened by the trainer and neither company ever had a top bar to
read.

## Findings table

No findings. Nothing was observed on any screen, so nothing is reportable as OBSERVED or SUSPECTED
without inventing evidence. The one environment note below is stated as a fact about the run, not as
an application defect.

| Severity | Screen | Steps | Expected | Actual | Evidence | Label |
|---|---|---|---|---|---|---|
| NOTE | (none — server) | `Invoke-WebRequest http://127.0.0.1:5097/` before and during the lane | App reachable for the whole lane | Reachable (HTTP 200) at lane start; unreachable afterwards, and did not return | `DOWN: Unable to connect to the remote server` | OBSERVED |
| NOTE | (none — tooling) | `chrome-devtools-axi open http://127.0.0.1:5097/` with `CHROME_DEVTOOLS_AXI_SESSION=alpha` | Command returns a page snapshot | Command returned no snapshot output and the session never became usable; the same root cause (server down) is the likely explanation but was not isolated before the run was stopped | no output from the `open` command | OBSERVED |

Note on `players-ready`: the file `reports/playtest/_playtest/players-ready` was **not** created. Both
players never got past registration, and writing the ready marker would have told the trainer lane
that two companies were waiting to play, which would have been false.

## Arithmetic checks

None possible. No auction cleared, no order filled, no OTC transfer and no surrender happened, so
there is no cash, position, cleared-price or holding arithmetic to check.

## What worked well

- The brief and the common notes were complete and unambiguous about accounts, PIN, browser
  sessions, the `bid-volume` / price-slider controls, and the auction-window rule, so no clarification
  round-trip was needed before starting.
- The shared notes correctly predicted the failure mode that the fixtures are read from the live
  app at `127.0.0.1:5097`, and the start-up probe (`200 OK`) correctly established that the app was
  live before the failure, which keeps the timeline unambiguous.
- The `chrome-devtools-axi` CLI exposes per-session isolation and `CHROME_DEVTOOLS_AXI_BRIDGE_TIMEOUT_MS`,
  so two simultaneous player sessions were going to be genuinely independent by construction.
- Per-session `stop` is idempotent and reports `status: stopped`, so teardown is deterministic and
  safe to run even for a session that never fully started.
- The `_playtest` directory and the year-file convention give the trainer and player lanes a clean
  rendezvous, so a stalled or aborted lane is detectable from the filesystem alone.

## What would confuse a first-time player

- This lane produced no evidence on this question. The item below is the one gap that made the abort
  invisible to the player flow, and it is a harness gap rather than a product finding.
- The player lane had no independent liveness check on the app. The lane read as "running" because
  the browser session was alive, while the thing being tested was gone. A single probe before each
  year (and a retry rather than a silent no-snapshot) would have turned a hang into a clear
  "server down" message.
- The brief does not say what to do when the server is down, only that the trainer lane may stall. A
  player lane hitting the same outage has no defined abort path, and without one it is tempting to
  keep polling a dead host.

## Recommended follow-up

- Re-run this lane against a server that stays up, with the low-memory pressure removed (or with the
  app hosted elsewhere) so the eight briefed actions and the year-1/2/3 cadence are actually covered.
- Add a server-liveness probe to the player lane's loop, and a rule that an unreachable app aborts the
  lane immediately with a partial report instead of retrying silently.
- Consider running the player lane against the hosted demo rather than a local process, which removes
  the server as a failure domain entirely.

