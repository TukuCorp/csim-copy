# Architecture pass, round E (from Claude): Standing, Engine side only. Nothing else this round.

D0 accepted (good catch on the leak, and the two-company test). SystemReport sold == bought: accepted as
intended; add the identity-pinning test in round F, not now.

C3 slipped twice because it was one big item. Split it. THIS round is ONLY E1-E3; Web is round F.
Full design and my decisions on definitions are in reports/arch/refactor-03.md section C3: use them as written.
Work in small green steps and run the golden after each step; if you are running short, stop after a green
step and report what is wired and what is left rather than landing something half-wired.

## E1 The module, with its own tests, wired to nothing yet
Create Standing (Engine, CONTEXT.md term) with a small interface: for a company (or unit) and a year, live or
settled -> emissions the obligation is measured on, allowances held (escrow included), allowances available,
offsets held, offsets usable (capped by the year's limit, computed once here), position (signed, long positive),
shortfall (>= 0), covered, settled? and the settlement when it is. Settled years answer from the settlement.
Table-driven Engine test first (red): held vintages old/current/future, escrowed vs available, offsets under and
over the limit, settled vs live, multi-unit company. Make it green. Commit nothing.

## E2 Register lookup
ComplianceRegister gets a company-year lookup (one dictionary), used internally and exposed for callers; replace
every Results.FirstOrDefault(r => ReferenceEquals(r.Company, c) && r.Year == y) in the Engine with it (Scoring x4,
SystemReport, ...). The 2 scans in PlayerSession are Web: leave them for round F.

## E3 Move the Engine callers onto Standing, one at a time, golden after each
Order: Scoring (Holdings, FinalLongShortPosition), ComplianceRegister (obligation/offset limit), SystemReport,
then ComplianceBot last. For the bot: Shortfall and HeldAllowances ask Standing; the unrounded
BausEmissions x OffsetUsageLimit used in Quote and Settle stays as a bot-local pricing estimate, named as
such and covered by a test that it drives price only, not whether the bot is compliant. After each caller the
golden (both difficulty hashes) must be byte-identical; if one moves, stop at that caller, list the difference
and which rule was right, do not re-baseline silently.
Delete CompliancePosition if nothing uses it afterwards (and its 3 test constructions); otherwise say what is left.

## Report
Per step: green or not, the count of `new CompliancePosition(` and `Results.FirstOrDefault` before/after, lines
added/removed, Engine/Data/Web test counts, golden status. Usual command.
