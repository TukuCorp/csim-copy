# csim-copy — CarbonSim behavioural clone

## What this project is
A from-scratch, behaviourally faithful clone of EDF's CarbonSim, the multi-user emissions-trading-system (ETS) training game used in the Vietnam ETS Training & Simulation TA (ETP/UNOPS, VNEEC, 2023–2025). Players run virtual companies that must comply with a declining cap at least cost using abatement, government auctions, an exchange, and OTC trades, alongside rule-based AI bots. An administrator configures the ETS and drives the clock.

The original is proprietary and its terms forbid reverse engineering and derivative works. **We build from public documentation and observed behaviour only.** Never copy code, CSS, JavaScript, text strings or assets from carbonsim.org; the harvested material under `research/` is a specification reference, not content to ship.

## Where the knowledge lives
- `research/2026-09-13_carbonsim-replication-assessment.md` — what CarbonSim is, what we know, confidence levels.
- `research/2026-09-13_tech-stack-reference.md` — original stack and the chosen modern stack.
- `research/2026-09-13_carbonsim-video-insights.md` — screen-by-screen walkthrough of the player UI and admin console (acceptance script for the UI).
- `research/2026-09-13_bot-logic-and-numeric-fidelity-options.md` — bot design and calibration envelopes.
- `research/2026-09-13_etp-deliverables-availability.md` — what public deliverables exist.
- `research/carbonsim-sources/` — EDF spec/FAQ/glossary/cheat sheet, Vietnam trainer decks, exercise reports (Dominican Republic, Mexico), UI string catalog (`sim3-instance/resourcestrings_en.json`, 883 keys), video transcripts and screen frames, ETP deliverables.
- `background/` — the six Vietnam TA reports (markdown).
- `activeContext.md` — the live build plan with checkable items; update it as work progresses.

## Tech stack (decided 2026-09-15)
| Layer | Choice |
|---|---|
| Runtime | .NET 10 LTS (install with `winget install Microsoft.DotNet.SDK.10`; machine currently has 9.0.312, EOS Nov 2026) |
| Language | C# 14, nullable enabled, warnings as errors |
| Engine | `src/CarbonSim.Engine` — pure class library, no web/DB dependencies |
| Persistence | EF Core 10; SQLite for dev/test and single-box training deployments; PostgreSQL provider kept switchable |
| Web host | ASP.NET Core 10 (`src/CarbonSim.Web`) |
| Real-time | ASP.NET Core SignalR; one hub group per simulation; a hosted `SimulationClockService` pushes via `IHubContext` |
| UI | Blazor (interactive server render mode) for player and admin screens. Assumption: chosen for single-language TDD and server-owned game state; revisit before Phase 3 if a React front end is preferred |
| Charts | Apache ECharts via JS interop (candlestick, close-price line, MACC bars, N.O.P. bars, intensity gauge) |
| Tables | QuickGrid |
| Localisation | `.resx` resources keyed like the original catalog; culture cookie; English first, Vietnamese second |
| Tests | xUnit + FluentAssertions; bUnit for Blazor components; Playwright for end-to-end |
| Formatting | `dotnet format`; EditorConfig committed |

## Solution layout
```
CarbonSim.sln
src/CarbonSim.Engine/        rules, entities, auction clearing, order book, reconciliation, bots
src/CarbonSim.Data/          EF Core model, migrations, repositories
src/CarbonSim.Web/           ASP.NET Core host, SignalR hubs, Blazor UI, admin
tests/CarbonSim.Engine.Tests/
tests/CarbonSim.Data.Tests/
tests/CarbonSim.Web.Tests/
scenarios/                   JSON scenario files (sectors, firms, abatements, parameters)
research/                    source material (read-only)
```

## Working rules
- **Red/Green TDD for every feature.** Write the failing xUnit test first, run it and see it fail, then implement the minimum. Engine tests are deterministic: seed every random source.
- **Engine first, UI second.** No UI work on a mechanic until its engine behaviour has passing tests.
- **The string catalog is the naming authority.** Use `research/carbonsim-sources/sim3-instance/resourcestrings_en.json` to name screens, fields and events; write our own wording, do not paste EDF's.
- **Fidelity is defined by the acceptance envelopes** in the bot-fidelity brief (compliance, price paths, offset discount, leaderboard spread), not by matching EDF's numbers exactly.
- **Simplicity first.** Minimal diffs, no speculative abstractions, no temporary hacks; find root causes.
- **Verification before done.** `dotnet build` with zero warnings, `dotnet test` green, and for UI work a Playwright run against the demo-video acceptance script.
- Keep `activeContext.md` current: tick items as completed, add a review section after each phase.
- After any user correction, add the pattern and a preventing rule to `lessons.md`.
- Commit only when asked; never push to `main` from a feature branch without asking.

## Commands
```
dotnet build CarbonSim.sln
dotnet test CarbonSim.sln
dotnet run --project src/CarbonSim.Web
dotnet format CarbonSim.sln
```

## Domain vocabulary (short)
Allowance (vintage-dated), Offset (no vintage, usage limit), Cap, Free allocation %, BAU growth, Compliance obligation, Long/Short position, Abatement (upfront cost, annual reduction, implementation time, lifetime, $/t), MACC, Auction (sealed-bid uniform price, price collar), Exchange (market/limit/stop orders, partial fill, volatility band), OTC (seller-initiated offers), Surrender & Banking (banking capped at obligation), Penalty (cash multiplier + allowance debit), Marginal Cost of Compliance (leaderboard metric), Trading System (one ETS inside a simulation), Unit (emitting facility) and Company (one or more units), AI Unit (bot), Administrator.
