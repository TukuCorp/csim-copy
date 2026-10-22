# Playtest: shared notes for both lanes

Today is 2026-10-21. We are playtesting our own CarbonSim clone (a carbon-market training game) before a
teammate session on the hosted demo. The app runs locally at **http://127.0.0.1:5097** (a Release
build identical to the hosted one). Everything here is local test data.

## Test accounts (local only)
- Trainer: `trainer@carbonsim.demo`, password `rehearsal-trainer-1`
- Registration PIN for players: `rehpin`
- Players register themselves on the sign-in page (email, display name, password, company, PIN).
  Use `alpha@playtest.test` / `Alpha` / `playtest-alpha-1` and `bravo@playtest.test` / `Bravo` /
  `playtest-bravo-1`.

## Browser
Use the `chrome-devtools-axi` CLI (run `chrome-devtools-axi --help`). Give every browser identity its own
session so cookies do not mix, e.g. `CHROME_DEVTOOLS_AXI_SESSION=trainer`, `=alpha`, `=bravo`, and set
`CHROME_DEVTOOLS_AXI_BRIDGE_TIMEOUT_MS=90000` (first start is slow). Useful commands: `open <url>`,
`snapshot`, `click @uid`, `fill @uid text`, `fillform @uid=val ...`, `eval "() => ..."`, `wait <ms>`.
Stop your sessions at the end with `chrome-devtools-axi stop` (per session).

Known quirks (not bugs to report):
- Pages are Blazor Server; the screen re-renders live. A value typed with `fill` can be reset by a
  re-render before it is committed. If that happens, set it with `eval`: assign `.value` and dispatch
  a `change` event (and `input` for range sliders), then click the button in the same `eval`.
- The bid volume input has id `bid-volume`; the bid price is an `input[type=range]` (floor to ceiling).
- An auction window is open only at the start of each section of the year (the first 45%); "Place
  bid" is disabled outside it. Poll the snapshot every few seconds until it is enabled.
- uids change after every snapshot; always use uids from the latest snapshot.

## What to report
Anything that is broken, wrong, confusing or slow: a refused action without a clear reason, a number
that does not add up (cash, position, cleared price, holdings), a screen that freezes or shows the
reconnect banner, text in the wrong language, a control offered in a state where it cannot work. For each
finding: severity (BLOCKER / MAJOR / MINOR / NOTE), screen, steps to reproduce, expected, actual, and
the evidence (the exact text you saw). Label each one OBSERVED (you saw it happen) or SUSPECTED (you
infer it). Do not invent findings; "no issues on this screen" is a valid result.

## Hard rules
1. Only use http://127.0.0.1:5097. Never visit any other site with these accounts.
2. Do not edit any file in the repository except your own output file and files under
   `reports/playtest/_playtest/`. Do not run git commands that change anything.
3. Do not paste raw HTML or full snapshots into your output file; quote only the lines that matter.
4. Avoid the dollar sign in shell text. UTF-8 for all files.
5. Keep memory low: one browser session per identity, no extra tabs. The machine has ~1 GB free.
