# Gap Analysis: Shippable internal test demo of the CarbonSim clone

**Date:** 2026-10-16
**Scope:** `main` at `f341aab` (clean tree). Compares what the repo can do today against what an internal team needs to play a live test session of the clone on a hosted instance. Grounded in `activeContext.md`, `docs/handover.md`, `docs/deployment.md`, `docs/trainer-runbook.md`, `lessons.md`, `Program.cs`, `Dockerfile`, `appsettings*.json`, and a fresh build and test run.
**Status:** Resolved 2026-10-17, see "Resolution" at the end. Docker was dropped from the stack, so GAP-02 was closed by deletion.

---

## Executive Summary

The product is close to feature-complete for a demo. The engine, persistence, player UI (11 screens), admin console, Vietnamese localisation, accessibility pass, load test (250 clients, 0 missed auctions) and seven QA rounds are all done, and the suite is green. What's missing is mostly **delivery**, not features. No instance is running anywhere testers can reach. The one container path is broken: the `Dockerfile` `ENV` line has `+` where its `\` line continuations should be, and Docker was never run on this machine. There's no production-mode config or startup script, no tester-facing guide, and no way to collect feedback or diagnose problems after a session. That makes **2 CRITICAL and 3 HIGH gaps**, and all of them are small. Recommendation: one short "ship the demo" sprint (hosting, config, tester pack, a full rehearsal) before more fidelity or architecture work.

---

## Assumptions about the target

"Shippable internal test demo" is read as:

1. **Audience:** about 3–10 internal colleagues (not trainees, not the client), plus one person acting as trainer/admin.
2. **Access:** testers use their own browsers on their own machines. They don't run `dotnet run` and don't sit at the dev box.
3. **Session:** one exercise from `scenarios/vietnam-2024.json`, played through at least 2–3 virtual years (20 min each, or shorter if the trainer sets that in setup), with bots filling the other companies.
4. **Quality bar:** nothing crashes, a restart can be recovered from, testers know what to do and what's a known limit, and findings come back in a form that can be acted on.
5. **Out of scope:** commercial launch, the EDF licensing question, the .NET 10 upgrade, the full 240-unit two-day training agenda, and closing the remaining fidelity envelopes.

---

## Current Capabilities (What We Have)

| Capability | Status | Key Surfaces |
|---|---|---|
| Engine: clock, allocation, abatement, auction, exchange, OTC, compliance, finance, scoring, bots | Mature | `src/CarbonSim.Engine/` (Run/, Allocation/Standing.cs, Market/, Bots/) |
| Fidelity harness and golden | Mature (2 envelopes skipped with their measurements) | `tests/CarbonSim.Engine.Tests/FidelityTests.cs`, `Golden/fidelity-golden.md` |
| Save/reload of a whole run, SQLite backup drill | Mature | `src/CarbonSim.Engine/Snapshot/`, `src/CarbonSim.Data/`, `SqliteBackupRestoreTests.cs` |
| Accounts: PIN-gated registration, company claim, login, reset codes | Working | `src/CarbonSim.Web/Accounts/`, `Endpoints/SignInEndpoints.cs` |
| Player UI, 11 screens, EN + VI, phone width, axe-clean | Mature | `src/CarbonSim.Web/Components/Pages/`, `Resources/SharedStrings*.resx` |
| Admin console: setup, clock controls, collar change, fines/offsets/abatement/shock, reports + CSV, save/load/delete | Mature | `src/CarbonSim.Web/Components/Admin/`, `src/CarbonSim.Web/Admin/` |
| Live updates (server-side 1 s refresh + SignalR hub) | Working | `Simulations/SimulationRegistry.cs`, `SimulationClockService.cs` |
| Load capacity | Measured: 250 clients, 0 missed auctions, p99 refresh 15 ms | `tests/CarbonSim.Web.Tests/Load/` |
| Dev demo boot (`dotnet run`, seeded exercise, dev admin + PIN) | Working, **localhost only** | `appsettings.Development.json`, `Properties/launchSettings.json`, `DemoSimulationSeeder.cs` |
| Container deployment | **Broken, never built** | `Dockerfile` (line 24), `docker-compose.yml` |
| Production-mode config / hosted instance | Missing | `appsettings.json` (empty PIN, no admin) |
| Trainer runbook, deployment notes, handover | Working (handover partly stale) | `docs/trainer-runbook.md`, `docs/deployment.md`, `docs/handover.md` |
| Tester guide, feedback capture, session diagnostics | Missing | none |

Test gate observed today: see the **Verification** section at the end.

---

## Target State

> An internal tester gets a URL, a PIN and a one-page guide. They register, claim a company and play a multi-year exercise against bots and each other while a colleague drives the clock from the console. The host keeps running, or can be recovered within minutes, for the whole session. Testers know which behaviours are deliberate limits. Every problem they hit comes back as an actionable report backed by server logs, and the team can rebuild the same instance from the repo with one documented command.

---

## Gap Analysis

### GAP-01: No reachable hosted instance

**Severity:** CRITICAL. Testers can't play a demo they can't reach.

**Current state:** The only working way to run the app is `dotnet run --project src/CarbonSim.Web`. That binds `http://localhost:5003` in Development mode (`src/CarbonSim.Web/Properties/launchSettings.json`). Every QA round in `reports/qa/` ran against localhost. Docker isn't installed on the dev machine (`docker: command not found`). No cloud, VM or LAN host is documented as in use.

**What's needed:**
- Pick a host: (a) the dev box with `ASPNETCORE_URLS=http://0.0.0.0:<port>` plus a firewall rule or a tunnel (Tailscale or Cloudflare Tunnel) that terminates TLS and passes WebSockets, or (b) a small Linux VM or container service running the image once GAP-02 is fixed.
- WebSocket upgrade and long idle timeouts end to end. The Blazor circuit and `/hubs/simulation` both depend on them (`docs/deployment.md` §6).
- A stable URL that testers get ahead of time.
- A smoke check against the hosted URL: `/sign-in` 200, register with the PIN, admin console loads, one auction clears.

**Existing assets to reuse:**
- `docs/deployment.md` covers single-server deployment, TLS/reverse proxy and backups.
- `Dockerfile` and `docker-compose.yml` (after GAP-02).
- The Playwright host fixture (`tests/CarbonSim.Web.Tests/Components/PlaywrightHostFixture.cs`) already boots the real host and walks it. It could point at a base URL for the smoke check.

**Effort estimate:** 1 short plan (2 phases: choose and provision the host, then the smoke check). Low complexity. The work is mostly ops.

---

### GAP-02: Container build is broken and unverified

**Severity:** CRITICAL if a container is the chosen hosting route; HIGH otherwise. The one reproducible deployment artifact fails as written.

**Current state:** `Dockerfile` line 24 reads `ENV ASPNETCORE_HTTP_PORTS=8080 +    DOTNET_EnableDiagnostics=0 +    CarbonSim__Provider=Sqlite + ...`. The `\` line continuations were mangled into `+` on one line, so `docker build` either rejects the line or sets malformed variables. In that case `CarbonSim__ScenarioFile=/app/scenarios/...` and the `/data` connection string never take effect, and the host falls back to `../../scenarios/vietnam-2024.json` relative to `/app`. That path doesn't exist, so the host fails at the first scenario read. The image was never built (`activeContext.md` Phase 6: "Docker is not installed ... the image was NOT built or run"). Separately, the build stage copies `Directory.Build.props` and `Directory.Packages.props` but not `.editorconfig`. With `EnforceCodeStyleInBuild` and warnings-as-errors, the container build runs under different analyzer severities than the local gate.

**What's needed:**
- Fix the `ENV` line (restore the `\` continuations or split it into separate `ENV` lines).
- Copy `.editorconfig` into the build stage so the container build applies the same rules.
- Build and run the image on a machine that has Docker (or in CI). Check `/sign-in`, admin bootstrap through `CarbonSim__AdminEmail`/`__AdminPassword`, and that SQLite in the `/data` volume survives a container restart.
- Optional: add a `docker build` step to a CI workflow so the file can't silently rot again.

**Existing assets to reuse:** `Dockerfile` (multi-stage, non-root, the rest looks sound), `docker-compose.yml` (SQLite volume, optional Postgres profile), `docs/deployment.md` §5.

**Effort estimate:** Part of the GAP-01 plan (1 phase). Small, but it needs a Docker-capable machine.

---

### GAP-03: No production-mode demo configuration or launch script

**Severity:** HIGH. Running the demo in Development mode leaks dev credentials and detailed errors, and running it in Production mode as shipped fails in other ways.

**Current state:**
- `appsettings.json` (Production) has `RegistrationPin: ""` and no `AdminEmail`/`AdminPassword`, so nobody can register and nobody can reach the console until both are configured (`docs/trainer-runbook.md` §2.1).
- `ScenarioFile` defaults to `../../scenarios/vietnam-2024.json` relative to the content root (`CarbonSimHostOptions.cs:44`, resolved in `ScenarioCompanyCatalog.cs:27`, `AdminSession.cs:122`, `DemoSimulationSeeder.cs:37`). That works only when the host runs from `src/CarbonSim.Web`, not from a `dotnet publish` folder.
- The only working preset is `appsettings.Development.json`: a known PIN (`carbonsim-dev-pin`), a known admin password (`carbonsim-dev-admin`), `DetailedErrors` on, and reset codes kept in memory. That's acceptable on localhost but not on a URL shared beyond the dev box.
- The registration open/close switch and the PIN set from the console live in memory (`RegistrationPolicy`), so a host restart reverts them to configuration.

**What's needed:**
- A dedicated `appsettings.Demo.json` (or an env-var block in the compose file) with a demo PIN, a demo admin and an absolute or content-relative `ScenarioFile` that resolves from the publish folder, with `StartDemoSimulation` either on or off by deliberate choice. Secrets go in user secrets or env vars, not in git.
- A one-command launcher (`scripts/run-demo.ps1` or the compose file) that publishes, sets the environment and starts the host on the chosen bind address.
- `app.UseForwardedHeaders()` if TLS terminates at a proxy or tunnel. `docs/deployment.md` §6 assumes forwarded headers but `Program.cs` never registers the middleware.

**Existing assets to reuse:** `CarbonSimHostOptions` (every key already exists and is validated at boot in `Program.cs`), the config reference in `docs/deployment.md` §3, and the sample block in `docs/trainer-runbook.md` §2.1.

**Effort estimate:** 1 phase. Low complexity.

---

### GAP-04: No tester pack (guide, known limits, accounts, script)

**Severity:** HIGH. Without one, testers report deliberate limits as bugs and miss the flows the demo is meant to exercise.

**Current state:** `docs/trainer-runbook.md` is written for the trainer and the two-day room. Nothing in the repo is written for a player. The tester needs to know how to register (PIN, claiming a company, the 37 human companies), what each screen is for, a short "things to try" list (bid at auction, rest an exchange order, send an OTC offer, implement abatement, surrender and bank), and which behaviours are accepted limits. The accepted limits live only in `activeContext.md` ("Review – the QA loop"):
- only about 40% of company-years comply, so penalties in year 3 are expected;
- the exchange sell side is thin by construction;
- a mid-run shock moves only the collar;
- a restart mid-auction can replay one notice.

**What's needed:**
- `docs/tester-guide.md`: access, registration, a 10-minute tour, a task checklist per role (player and admin), and a known-limits box in plain language.
- A session plan: who plays which company, who drives the clock, the year length to set (for example 10 min to fit a 1-hour slot), and when to pause for questions.
- English first. A Vietnamese version only if VN-speaking testers are in the session.

**Existing assets to reuse:** `docs/trainer-runbook.md` §§3–5 and §8 (agenda, clock driving, screen talking points, fidelity promises), the QA screenshots in `reports/qa/*.png`, and the acceptance script in `research/2026-09-13_carbonsim-video-insights.md`.

**Effort estimate:** 1 phase. Writing only, no code.

---

### GAP-05: No feedback capture or session diagnostics

**Severity:** HIGH. An internal test is worthless if what testers hit can't be reproduced afterwards.

**Current state:** Logging goes to the console only (default `ILogger`, no file sink; `Program.cs` has no `UseExceptionHandler` and no file logging). In Production, `DetailedErrors` is off, so a circuit failure shows the tester Blazor's generic reconnect/error bar and leaves only a console line on the server. The engine is seeded and replayable (`SimulationRandom`, the golden, `SimulationSnapshots`), but nothing tells a tester or the team the seed, run id, year and wall-clock time to attach to a report. There's no feedback form, issue template or shared sheet.

**What's needed:**
- Persist logs to a rolling file, or the container log driver, for the duration of the session. Keep the reset-code `Log` sender working.
- A visible build/run stamp, for example the run id, seed and app version in the console footer and on the Rules panel, so reports can be tied to a run.
- One feedback channel with a template (screen, what you did, expected vs got, time, run id). A GitHub issue template in `TukuCorp/csim-copy` or a shared sheet both work.
- After the session: keep the SQLite file (`VACUUM INTO` drill, `docs/deployment.md` §7) and the logs as evidence under `reports/qa/`.

**Existing assets to reuse:** the `reports/qa/feedback-0N.md` format from seven QA rounds, the `SqliteBackupRestoreTests` drill, and the admin CSV exports (`AdminCsv`).

**Effort estimate:** 1 phase. Low to medium complexity: a logging provider, a footer stamp with a bUnit test, and a template.

---

### GAP-06: No end-to-end rehearsal on the target host with real people

**Severity:** MEDIUM. Every piece is tested, but nobody has played the whole session over a real network.

**Current state:** The Playwright admin walk drives year 1 → year 2 on localhost (`PlaywrightAdminTests.cs`). The load harness uses simulated clients in-process (`tests/CarbonSim.Web.Tests/Load/`). No run has gone through all 3 years with several human browsers on a remote URL, and nobody has exercised the recovery path (kill the host mid-year, restart, **Load** the saved exercise, resume) on the demo host.

**What's needed:**
- A 30–45 minute dry run on the hosted instance: 2–3 people, short years, all three years to the leaderboard, CSV export, plus one deliberate restart and reload.
- Record the outcome as `reports/qa/feedback-08.md` and fix anything that blocks before the real session.

**Existing assets to reuse:** `docs/trainer-runbook.md` §2.3 ("Rehearse the room") and §7 (Recovery), and the Playwright walks, which can be re-pointed at the hosted URL as a smoke check.

**Effort estimate:** Half a day of people time, plus fixes as they come up.

---

### GAP-07: Repo hygiene and stale handover before tagging a demo build

**Severity:** MEDIUM. It doesn't block play, but it misleads whoever picks up the demo build.

**Current state:**
- `admin-session.bak` (608 lines) and `standing.bak` (193 lines) are committed at the repo root by the `f341aab` "auto backup" commit. They're pre-refactor copies of source files.
- `docs/handover.md` §5–6 is stale. It says four fidelity envelopes are skipped and lists bot calibration as the top next step, but `activeContext.md` shows two skipped, the price path closed, the shock envelope closed, and the run module, Standing and band work done.
- `activeContext.md` line 3 still says "awaiting go-ahead to start Phase 0". The "Decide Blazor vs React" item is still unticked even though Blazor has shipped.
- There's no tag or release marking the demo build.

**What's needed:**
- Remove the two `.bak` files and add `*.bak` to `.gitignore` (with the user's go-ahead, since the backup job created them).
- Refresh `docs/handover.md` §5–6 and the `activeContext.md` header to the current state.
- Tag the demo commit (for example `demo-internal-1`) so testers' reports map to a build.

**Existing assets to reuse:** the review sections in `activeContext.md` already hold the correct current state.

**Effort estimate:** Under 1 phase. Trivial.

---

## Second-Tier Gaps

| Gap | Severity | Summary | Existing Assets |
|---|---|---|---|
| GAP-08 Fidelity narrative | MEDIUM | Only 294 of 726 company-years comply at the shipped calibration, and year 3 has penalties on most units. Testers will read that as "the game is broken" unless it's framed (GAP-04) or a demo-friendly calibration (the 6% offsets row: 73% compliant, flatter prices) is offered as a second scenario file. | Calibration grid in `activeContext.md` (QA round 6), `scenarios/tools/set-allocation-factors.ps1`, the fidelity harness |
| GAP-09 Restart durability | MEDIUM | Live runs are in memory and saved at year end and after each control. A crash loses up to the current year, and the registration toggle/PIN reverts to config. Acceptable for a demo if the recovery steps are rehearsed (GAP-06). | `docs/trainer-runbook.md` §7, the **Load** action on saved exercises |
| GAP-10 No CI | MEDIUM | The gate runs only locally. No GitHub Actions workflow runs build, test or `docker build`, so a demo tag isn't independently verified. | `dotnet build`/`dotnet test` commands; Playwright can install in CI |
| GAP-11 Password reset UX | LOW | Reset codes only go to the log or memory, so a tester who forgets a password needs the operator to read the log. Fine internally if the guide says so. | `IEmailSender`, `LoggingEmailSender` |
| GAP-12 Per-refresh account lookup | LOW | One SQLite round trip per client per second, logged by EF. Harmless at 10 testers, but noisy in the log GAP-05 wants. | `docs/handover.md` §5. Fix: cache the account on the scoped `PlayerSession` |
| GAP-13 `tradingOpenShareOfYear` inert | LOW | Shown and editable in setup but ignored by the clock, so a tester who changes it sees no effect. | `Parameters.cs:65`, `SimulationClock.cs` |
| GAP-14 AutoTrade rebuilds the fleet | LOW | Toggling AutoTrade re-draws every bot's trigger times. Invisible to testers, but it changes the seeded replay. | `AdminRun.razor:349`, `SimulationRegistry.RebuildBots` |
| GAP-15 .NET 9 target | LOW | .NET 9 support ends Nov 2026. Not a demo blocker; it's one line in `Directory.Build.props`. | `Directory.Build.props` |

---

## Recommended Sprint Sequencing

| Priority | Gap | Rationale |
|---|---|---|
| Sprint 1, day 1 | GAP-03 (demo config + launcher), GAP-02 (fix `Dockerfile`) | Everything else deploys these. Both are small and code-local, with a test for scenario-path resolution from a publish folder. |
| Sprint 1, day 1–2 | GAP-01 (host + smoke check) | Depends on GAP-03 (and on GAP-02 if containerised). Fastest route: the dev box plus Tailscale or Cloudflare Tunnel, then a VM later. |
| Sprint 1, day 2 | GAP-05 (logs, run stamp, feedback template) | Must be in place before anyone plays, or the first session's findings are lost. |
| Sprint 1, day 2 | GAP-04 (tester guide) + GAP-08 framing | Writing work that can run in parallel with GAP-01/05. |
| Sprint 1, day 3 | GAP-07 (hygiene, handover refresh, tag) | Cut the demo tag after the code changes above. |
| Sprint 1, day 3 | GAP-06 (dry run incl. restart) | The last gate before the internal session. Its findings feed back into the fixes. |
| Backlog | GAP-09–15 | Address based on what the internal session turns up. GAP-10 (CI) is the first one worth taking. |

---

## Risk Register

| Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|
| Tunnel or proxy drops WebSockets or has short idle timeouts | Blazor circuits reconnect constantly, screens freeze, actions get lost | M | Use a tunnel or proxy known to pass WebSockets and set idle timeouts in minutes (`docs/deployment.md` §6). Include a 10-minute idle test in the smoke check |
| Dev box sleeps, reboots or updates mid-session | Live run is lost back to the last save | M | Disable sleep and updates for the session window, rehearse **Load** recovery (GAP-06), or host on a VM |
| Testers judge fidelity on year-3 penalties | Negative feedback aimed at a documented calibration limit | H | Known-limits box in the tester guide. Optionally offer the 6%-offsets scenario as a second file (GAP-08) |
| Dev credentials reused on a shared URL | Anyone with the URL can reach the console | M | Separate demo admin and PIN through env vars or user secrets (GAP-03). Close registration once teams have claimed |
| A fix during sprint 1 shifts the seeded golden or a screen figure | Hidden regression right before the demo | L–M | Follow `lessons.md` §§8–10: run the full suite after the last edit, check both sides of any sign, list moved screen figures, and keep the golden byte-identical unless the move is deliberate |
| Docker image still fails on first real build (other untested lines) | Container route slips | M | Use the dev-box route for the first session and treat the image as a follow-up |
| The original's licence terms | Reputational or legal exposure if the demo leaves the company | L (internal) | Keep the demo internal. The EDF licensing item in `activeContext.md` "Parallel track" stays open before any external use |

---

## Suggested Next Step

Review this report. If the assumptions hold (internal colleagues, remote browsers, one multi-year exercise), invoke `/plan` once for **"Ship internal demo: GAP-01, GAP-02, GAP-03, GAP-05"** as a single 3–4 phase plan. GAP-04 and GAP-07 can run alongside it as writing tasks. Schedule GAP-06, the dry run, as the exit gate. Leave the fidelity and architecture backlog (bot plan/execution split, persistence mappers, the compliance envelope) until after the internal session has produced real feedback.

---

## Verification

Gate run at the time of writing (2026-10-16, `main` @ `f341aab`, .NET SDK 9.0.312):

- `dotnet build CarbonSim.sln`: 0 warnings, 0 errors.
- `dotnet test CarbonSim.sln`: green, 493 passed, 3 skipped, 0 failed.
  - Engine: 281 passed, 2 skipped (Envelope one, full compliance; Envelope five, 2–3% offset share).
  - Data: 16 passed.
  - Web: 196 passed, 1 skipped (the opt-in 250-client full load run). The Playwright walks ran for real.
- Checks not run: `docker build`, because Docker isn't installed. The `Dockerfile` defect in GAP-02 was found by reading the file (`cat -A Dockerfile`, line 24), not by building it.

---

## Resolution (2026-10-17)

The user removed Docker from the stack and asked for every remaining gap to be addressed. The work
and its evidence are in `activeContext.md` ("Internal demo - close the gaps" and its review).

| Gap | Outcome | Where |
|---|---|---|
| GAP-01 Hosting | Done, apart from opening a tunnel, which needs the operator's account. The launcher binds 127.0.0.1 for a tunnel or 0.0.0.0 for the LAN | `scripts/run-demo.ps1`, `docs/deployment.md` §5 |
| GAP-02 Dockerfile | Closed by deletion. No containers in the stack | `CLAUDE.md` Deployment row |
| GAP-03 Demo config | Done: Demo environment, scenarios shipped in the build with one path resolver, loopback-only forwarded headers, generated credentials | `appsettings.Demo.json`, `CarbonSimHostOptions.ScenarioPath`, `Program.cs` |
| GAP-04 Tester pack | Done | `docs/tester-guide.md` |
| GAP-05 Diagnostics | Done: a timestamped log file per start, a build/run/seed footer stamp, the Blazor error banner, an issue template | `BuildStamp.razor`, `App.razor`, `.github/ISSUE_TEMPLATE/demo-feedback.yml` |
| GAP-06 Rehearsal | Automated part done (publish, play, kill, restart, reload). The human dry run is a checklist for the operator | `PublishedDemoTests.cs`, tester guide §9 |
| GAP-07 Hygiene | Done: `.bak` files deleted and ignored, handover and plan header refreshed. No tag yet: nothing is committed | `.gitignore`, `docs/handover.md` |
| GAP-08 Fidelity framing | Done in the guide's known-limits list | tester guide §6 |
| GAP-09 Restart | Documented accurately (saves happen at creation and year end, not "after each control") and rehearsed by the E2E test | runbook §7, deployment §1 |
| GAP-10 CI | Workflow written. Not yet run on a hosted runner | `.github/workflows/ci.yml` |
| GAP-11 Password reset | Operator procedure written and run live. There is still no reset page | deployment §8 |
| GAP-12 Account lookup | Done: one read per circuit | `PlayerSession.CurrentAccountAsync` |
| GAP-13 `tradingOpenShareOfYear` | Done: derived from the auction window, column dropped | `Parameters.cs`, migration `DeriveTradingOpenShare` |
| GAP-14 AutoTrade | Done: only the switched company's bot changes | `SimulationBots.Refresh` |
| GAP-15 .NET 10 | Not done. Installing the SDK is a user decision | — |

Gate after the last code edit: build 0 warnings, format clean, 504 tests passed, 3 skipped, 0 failed, with Playwright running for real. The fidelity golden is unchanged.
