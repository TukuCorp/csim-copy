# Playtest: trainer lane (partial)

## Summary

- Did 3 years complete? **No**.
- Total time played: **0 minutes of exercise time** (no year was ever started).
- Years completed: **0 of 3**.
- Findings recorded: **2** (both BLOCKER, both environmental).
- Result: **PARTIAL / BLOCKED.** The local CarbonSim instance at `http://127.0.0.1:5097` was shut
  down for low memory part-way through the lane and was not expected to come back in this run.
  The browser work therefore could not be started.

## Timeline

| Clock time | Year | Auction | Action taken | What the screen showed |
|---|---|---|---|---|
| 2026-10-21, before any browser work | n/a | n/a | Probe `GET http://127.0.0.1:5097` | HTTP 200, Blazor Server page served (title `CarbonSim`, `<base href="/">`, Blazor server component descriptor present). Server was reachable at this point. |
| 2026-10-21, before any browser work | n/a | n/a | Read `reports/playtest/playtest-brief-trainer.md` and `reports/playtest/playtest-common.md`; listed `reports/playtest/_playtest/` | `_playtest/` contained only the directory entry (10 bytes). No `players-ready`, `exercise-created`, `year-1-started`, `year-2-started` or `year-3-started` marker file existed. |
| 2026-10-21, before any browser work | n/a | n/a | `chrome-devtools-axi --help` (session `trainer` not yet started) | CLI available with the documented commands (`open`, `snapshot`, `click`, `fill`, `eval`, `wait`, `stop`, ...). |
| 2026-10-21, attempt to start browser | n/a | n/a | `CHROME_DEVTOOLS_AXI_SESSION=trainer chrome-devtools-axi open http://127.0.0.1:5097` | Command produced no output and did not return a snapshot before the run was stopped. The session was left in a non-started state. |
| 2026-10-21, after stop instruction | n/a | n/a | Re-probe `GET http://127.0.0.1:5097` | `Unable to connect to the remote server` - server confirmed down. |
| 2026-10-21, after stop instruction | n/a | n/a | `chrome-devtools-axi stop` for sessions `trainer`, `alpha`, `bravo` | All three returned `status: stopped (no-op)`. No browser process left running from this lane. |
| 2026-10-21, after stop instruction | n/a | n/a | Marker files | No marker file was written: `exercise-created`, `year-1-started`, `year-2-started`, `year-3-started` do not exist, because no exercise was created and no year was started. |

## Findings table

| Severity | Screen | Steps to reproduce | Expected | Actual | Evidence | Status |
|---|---|---|---|---|---|---|
| BLOCKER | Environment / app host | Start the local app, then probe `http://127.0.0.1:5097` from another process | The training instance stays up for the whole playtest so the trainer lane can create an exercise and run three years | The instance was shut down for low memory before the trainer lane could create the exercise; it did not come back in this run | `SERVER: DOWN Unable to connect to the remote server` (post-stop probe). Pre-stop probe returned `STATUS: 200` with the Blazor Server shell | OBSERVED |
| BLOCKER | Trainer console | `CHROME_DEVTOOLS_AXI_SESSION=trainer chrome-devtools-axi open http://127.0.0.1:5097` | A snapshot of the sign-in page is returned so the trainer can sign in with `trainer@carbonsim.demo` | The `open` call returned no output and no snapshot, so no sign-in was ever possible | Command completed with empty stdout and no snapshot line | OBSERVED |

No other findings. The trainer console screens (run view, reports, surrender status, leaderboard or system
report, messages, shock controls) were never rendered, so no statement is made about them in either
direction. The lesson checks in the brief were therefore not exercised:

- a year must not start by itself - **NOT TESTED**
- a year whose time is over must not be resumable, with a stated reason - **NOT TESTED**
- a control must not be offered in a state where it cannot act - **NOT TESTED**

## Final leaderboard (top 5)

Not available. No exercise was created, no year was played, so there is no leaderboard, no system
report, and Alpha and Bravo were never ranked.

## What worked well

- The brief and the shared notes were unambiguous about accounts, the registration PIN, the marker-file
  handshake between the two lanes, the reporting format and the mandatory final report-back command.
- The app itself responded correctly while it was up: the first probe returned HTTP 200 with a valid
  Blazor Server shell and the expected `CarbonSim` title, so the deployment was healthy at the start.
- The `chrome-devtools-axi` CLI was installed and its documented command surface (`open`, `snapshot`,
  `click`, `fill`, `eval`, `wait`, `stop`) was available, so the tooling was not the blocker.
- The marker-file convention gives the lanes a cheap, reliable rendezvous: the missing
  `players-ready` file was immediately visible in a single directory listing rather than being inferred
  from a timeout.
- Session isolation via `CHROME_DEVTOOLS_AXI_SESSION` works as documented and `stop` is idempotent,
  so cleaning up after an aborted run is safe and leaves no orphan browser processes.

## Open questions

1. What was the peak memory footprint of the local build, and is the Release instance expected to hold
   three 6-minute years on this machine? A 1 GB free-memory machine was flagged in the shared notes.
2. Should the lanes have a fallback when the host is down (for example, an offline seed-data mode, or a
   saved snapshot of the trainer console pages)? Nothing in the briefs covers that case.
3. The brief required waiting up to 10 minutes for `players-ready` before creating the exercise. With the
   server down, no exercise can be created at all - it is unclear whether the exercise should be created
   before or after the players register in a rerun, since the console may require both players to exist.
4. Was `chrome-devtools-axi open` expected to return a snapshot within the 30 s it was given? A silent
   no-output result is worth confirming against a healthy server, since it hides the real error.
5. The three lesson checks (no self-starting year, no resumable expired year, no inert controls) remain
   untested and should be the first items of the rerun.

