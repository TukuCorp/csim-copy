# Resolving bot logic and numeric fidelity

Date: 2026-09-13
Context: the replication assessment rates "identical numeric behaviour" at ~30% because EDF's AI-player rules and firm datasets are private. This note lists the ways to close or sidestep that gap, what each yields, and what it costs.

## 0. Reframe the target first
The Vietnam Final Report judged the tool by whether "price dynamics and market behavior closely resembled real-world ETS markets", not by whether numbers matched anyone's bots. EDF's own FAQ says the AI is "reasonably deterministic", rule-based with "a fairly limited repertoire", has known artefacts (offsets sometimes mispriced, bots can "miss their turn"), and that abatement data is only "broadly relevant" to real industries. Matching those internals exactly would reproduce their bugs and their fictional data. The useful target is **behavioural equivalence inside measured envelopes**, which is achievable; **bit-for-bit parity** is neither achievable nor worth having.

## 1. Routes to the real thing (highest fidelity, external dependency)
| Route | What you get | Likelihood / cost |
|---|---|---|
| Ask EDF / Josh Margolis for a licence, the admin manual, bot documentation, the Vietnamese instance export | authoritative rules, datasets, translations | Contact is public (deck slide 31). EDF licenses the tool to partners (MEXICO2 clause in ToS). Medium likelihood; may involve a fee or partnership terms |
| Ask the Vietnam consortium (VNEEC, VETS, South Pole) and ETP/UNOPS for the Task 5/6 deliverable and training logs | the localized parameter sheets, per-session system reports, auction tables, leaderboards from six real runs | High value: six runs with ~600 human players is a calibration dataset no one else has. Depends on data-sharing terms of the TA |
| Host a paid or partnered CarbonSim session and observe it | live behaviour with our own parameters | Legitimate, but ToS forbids publishing results or reverse-engineering; use strictly as internal calibration with written consent |

## 2. Black-box inference from what is already public
We hold enough observed outputs to fit a bot model without seeing its code:
- Dominican report: 41 auction results (year, auction #, vintage, volume, clearing price, % awarded) across six years; system totals; a 60-unit leaderboard with cost of compliance and final positions.
- Mexico report: cap, 3%/yr, 90% free, 4 auctions, 100/300 collar, 10% offsets; compliance rates 74–84%; average allowance price 160–219; average offset 173–213.
- Demo video, year 1 of a 242-unit run: 46.6 Mt sold at auction, average allowance 123.55, average offset 92.63, 2.95 Mt abated, 9.29 Mt offsets surrendered, zero penalties, bot leaderboard spread from about +$37 to -$941M (bank artefact) with typical bots at $9–38/t.
- Slides 27–28: exchange price paths for a 3-year and a 10-year 90%-free run (prices climb from ~32 to ~48 and ~65; vintage spreads; offsets trade below).
- FAQ: bots act on preset triggers "at a certain point in the simulation", surrender offsets first, bank up to the obligation, abate when cheaper than market.

Method: build the engine with a parameterised rule-based bot, then run simulation-based calibration (grid or Bayesian search over bot parameters) until a 242-entity, 3-year run reproduces those aggregates within tolerance. This yields a bot that is statistically indistinguishable in the reported metrics, which is the only sense in which anyone will ever compare the two.

## 3. Design the bot from first principles (needed in every scenario)
A cost-minimising compliance agent that matches every documented behaviour:
1. **Position**: forecast shortfall = BAU emissions × (1+growth)^t − free allocation_t − banked − held offsets (capped at limit).
2. **Abatement rule**: at year start, implement options in ascending $/t while cost < expected allowance price × margin, implementation time ≤ remaining years, and capital available; never re-implement; treat reductions as additive (the original does).
3. **Auction rule**: bid volume = fraction f_a of remaining shortfall; unit price = min(next un-implemented MAC, expected price) ± noise, clipped to the collar; submit early in the window (the original's ties break on time).
4. **Exchange rule**: post limit orders within the volatility band around last trade; buy when short and price < reservation price; sell surplus when long and price > cost basis; use partial fills.
5. **Offset rule**: value offsets at allowance price × (1 − discount), discount ≈ 5–25% (observed range), capped by the 10% usage limit.
6. **Timing triggers**: act at fixed fractions of the year (matches "misses its turn" behaviour) with per-bot jitter so 200 bots do not act simultaneously.
7. **Difficulty knob**: maps to margin, noise and reaction speed (the original exposes "AI Difficulty" and "AI Distribution within Sector").
8. **Determinism**: seed the RNG per simulation so runs are reproducible for training and testing.

## 4. Borrow from the literature for the "smart" tier
Agent-based carbon-market models (ECMAS power-sector model, the arXiv "Carbon Market Simulation with Adaptive Mechanism Design" MARL environment, Taiwan cap-and-trade ABM) provide tested bidding and abatement heuristics and, if wanted later, an RL-trained bot tier. Not needed for parity with EDF, whose bots are simpler than these.

## 5. Replace the data problem with better data
EDF's firm dataset is fictional and admittedly approximate. For Vietnam, the July 2025 impact report already supplies sector emissions shares, MAC estimates for cement, steel and power, and price corridors, and the sim3 login page supplies the 39 firm names. A Vietnam-realistic dataset built from those is more useful for DCC training than EDF's Mexican-derived numbers, and it removes the dependency entirely.

## 6. Make fidelity testable
Write acceptance tests on a seeded 242-entity, 3-year run:
- 100% bot compliance with zero penalties in the base scenario.
- Auction clearing at or near the floor in year 1, rising toward the ceiling by year 3 as the cap tightens (Dominican pattern 150 → 300).
- Offsets trade at a 5–25% discount to current-vintage allowances.
- Later vintages price at a premium to current vintage; premium grows with cap decline.
- Abatement volume in year 1 of the order of 1% of the cap; offsets surrendered ≈ 2–3% of the cap.
- Leaderboard spread of bot costs of compliance in the +$5 to +$40/t band with a few negative outliers.
- Regulator shocks (cap cut, floor raise) move exchange prices within one virtual month.
These envelopes come straight from the harvested reports and videos and are the practical definition of "numerically faithful".

## Recommendation
Run route 1 (EDF and the Vietnam consortium) in parallel with sections 3, 5 and 6; they cost nothing to start and the engine needs a bot anyway. Use section 2's calibration once the engine exists. Reserve route 1c (observing a licensed session) for the case where the consortium cannot share logs, and only with written consent.
