# CONTEXT - what the words in this repository mean

The domain vocabulary, in this project's own words. The material under research/ is a specification
reference for how the original behaves; none of its wording ships, and none of it is quoted here.

## The simulation

- **Simulation** - one ETS in a box: the sectors, companies and units it covers, the cap and the years,
  the markets, and the clock. The engine's aggregate root. A simulation carries a seed, and the same
  seed always plays the same run.
- **Trading system** - the rules an administrator configured for a simulation: the cap and its annual
  reduction, the free allocation share, the collar, the auction schedule, the penalty, the offset and
  banking limits. A simulation runs one trading system today, and Simulation.Rules is its current rules.
- **Rules** - the trading system's parameters as they stand right now. The collar is the one part a live
  run may change; everything else is fixed when the exercise is built.
- **Collar** - the floor and the ceiling a tonne may trade between, in the auction and on the exchange.
- **Run** - one played simulation: the engine module that owns the order of operations, plus the host
  that drives it (the gate, the broadcast, the screens). SimulationRun is the module.
- **Section** - one auction cycle inside a virtual year: the gap players trade in, then that section's
  auction window. A year holds as many sections as it holds auctions.
- **Trading window** - the period an action is allowed in. A year trades only while it is running, and an
  auction takes bids only while its section's window is open. TradingWindow holds the rule, and a refusal
  is a typed error (TradeRefusedException).
- **Standing** - where one company stands in one year: what it emits, what it holds, and the position
  that follows. Candidate 2 of the architecture review; today the question is answered in five places.
- **Gate** - the host's one-writer rule: one semaphore per run, held while anything reads or changes it,
  so the driver's tick and a player's action cannot interleave.
- **Announcement** - what the host owes its watchers after a change: which auctions opened, closed or
  cleared, which trades filled, how the books and the run's state moved. The run module gathers it as a
  RunResult while the gate is held, and the host broadcasts it after letting go.

## The instruments

- **Allowance** - a tonne of CO2e the holder may surrender, dated with the vintage (the year it belongs
  to). Vintage 2 is next year's.
- **Forward vintage** - part of a later year's allowances sold early, in the previous year's last
  auction. How much is a run setting (Parameters.ForwardVintageShare).
- **Offset** - a credit with no vintage that may meet part of an obligation, up to the offset limit.
- **Free allocation** - the share of the cap handed to companies at the start of a year.
- **Abatement** - a project a unit commits to: upfront cost, annual reduction, implementation time and
  lifetime. A sector's menu of them is its marginal abatement cost curve.
- **Banking** - carrying unused allowances into the next year, capped at the obligation.
- **Compliance obligation** - the tonnes a company must surrender for a year.
- **Penalty** - the cash charge and the allowance debit for an obligation left uncovered.
- **Marginal cost of compliance** - the leaderboard's ranking metric: what one more tonne of obligation
  would cost the company.

## The people

- **Player** - a person playing a company, or an AI unit that plays itself.
- **Administrator** - the trainer: configures the exercise, drives the clock, changes the collar, fines a
  unit, hands out offsets, closes the year.
- **Bot** - the rule-based player that runs an AI company (or a human unit the trainer switched to
  automatic). Its decisions are recorded as BotActions, and the golden keeps a fingerprint of them.
