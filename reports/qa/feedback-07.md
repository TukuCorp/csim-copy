# QA feedback 07 (from Claude): final polish round. Everything below is small.

Verified live this round: M1 (after floor 100 -> 140 the ladder re-quotes 135.8-152.6, uncrossed, last trade 141.40),
M2 (Messages (1) arrives, console lists the change), M5 (ladder of 5 levels each side), K3 (halt + close
mid-auction cleared it first: I bid 400,000 @ 200, cleared at the new floor 140.00, paid 56,000,000),
full lifecycle year 1 -> 3 -> End the exercise -> "Simulation ended" banner and final leaderboard, a second
concurrent exercise created from /admin/setup, phone width 390px has no horizontal page scroll.
M3 (collar-only) and the documented compliance limit are accepted.

## N1 Chart legend overlaps the axis title on phone width
At 390px the exchange price chart legend ("Vintage 1/2/3 / Offset") wraps onto and over the "per tonne"
axis label and the top gridline (reports/qa/r6-phone-exchange.png). Reserve space (grid top / legend
position) so the legend sits above the plot area when it wraps. Same check for the MACC chart.

## N2 Language switch is still blue on the navy top bar
Live computed colour is the link blue, not light text (contrast about 3:1, and underlined). The
header links need light text (e.g. the same colour as the other top-bar labels) with a visible
focus ring. Please verify in the browser rather than from the stylesheet; a more specific rule may be
overriding it.

## N3 Setup form inputs
/admin/setup: most inputs have empty id/name (only registration PIN and cap have ids), so they have no
programmatic label; add ids and <label for>. Units are inconsistent with the rest of the console:
the form shows annual cap reduction 0.03, free allocation 0.9, offset limit 0.1, volatility 0.1, interest 0.07,
while the run console shows 3.00%, 90.0%, 10.0%. Show percentages as percentages (accept 3 or 3%),
store fractions internally. Add a short hint under each field.

## N4 Write the QA loop into the project record
- activeContext.md: add a "QA loop (2026-10-10)" review section: the seven rounds (reports/qa/feedback-01..07),
  what was fixed (list by id), what is accepted as a documented limit (compliance envelope: grid in the fidelity
  report; collar-only shocks), and any open issue you know of.
- lessons.md: nothing from the user, but add the two lessons that came out of this loop if you agree:
  (1) seed demo data on the shelf, never advance the clock at boot; (2) measure before declaring an
  envelope "mutually exclusive" - sweep every lever (here the abatement menu was the missing one).
- docs/trainer-runbook.md: make sure the price path and the Change the rules card are described as a
  trainer will actually see them now.

Red/green where code changes, no commits, keep :5003 serving, then report with the usual command.
Include final test counts.
