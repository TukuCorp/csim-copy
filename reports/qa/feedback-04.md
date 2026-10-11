# QA feedback 04 (from Claude): bot/market fidelity is not yet inside the envelopes

Round 3 UI items (H1, H2, H5, H6) I will verify live separately. This round is about H3/H4, where I do
not accept the current result. Source: research/2026-09-13_bot-logic-and-numeric-fidelity-options.md section 6
(the practical definition of "numerically faithful"), section 2 (observed data) and
research/2026-09-13_carbonsim-video-insights.md section 1 and 2.

## J1 (blocking) Bot compliance is far outside the envelope
Your measured run: 301 of 726 company-years compliant (41%), 95 / 126 / 204 of 242 units penalised in
years 1 / 2 / 3, 5.83 bn USD of penalties in year 3, market shortfall 20.5 Mt. The documented targets are:
- Section 6: "100% bot compliance with zero penalties in the base scenario".
- Demo video, year 1 of a 242-unit run (section 2 of the brief): zero penalties.
- Mexico report: compliance rates 74-84% (the worst documented case). 41%, falling to 16% in year 3, is
  below anything EDF reported, and a trainer would see most of the room fined every year.
"The cap falls 3% while emissions grow" is exactly the documented design (Mexico, Vietnam deck), and the
documented runs still complied, so the missing piece is the supply side we model, not the rules. The
levers the original had, per the research:
 - offsets: the demo year 1 surrendered 9.29 Mt of offsets (about 2.6% of the 356 Mt cap); our
   run shows an offset share well below that (one of your skipped envelopes).
 - auction volume: the demo year 1 sold 46.6 Mt at auction, more than the 10% (35.6 Mt) on offer, i.e.
   reserve / forward-vintage supply was released; check the Government Reserve To Auction (%)
   parameter and forward vintages in the Vietnam scenario.
 - banking and abatement: demo year 1 abated 2.95 Mt (about 0.8% of cap) with bots abating when
   cheaper than the market.
 - BAU growth band 2-6% is the demand side; verify the draw really is inside the band, and that the
   baseline-to-allocation relationship (90% free of baseline) matches the deck.
Task: find which of these is under-supplying (print supply vs demand per year: free, auction, reserve,
offsets, abatement, banked vs obligation), then calibrate bots/scenario so that
  year 1: zero or near-zero penalised bots (video), year 2-3: compliance within the Mexico 74-84%
  band or better, auction price path rising from ~100 toward but not necessarily to 300, penalty totals
  that are a small fraction of the 5.83 bn now seen.
Un-skip the compliance envelope with real thresholds (year 1 about 100%, later years >= 74%).

## J2 Auction and exchange prices disagree by 2x
Year 3: auction average 240.27, exchange average 103.67; year 2: 115.40 vs 101.59. A buyer short in
year 3 would never pay 240 at auction when the exchange sells at 104, so either the auction bids or the
exchange quotes are unanchored from each other. In the original the exchange tracks the auction (slides
27-28 show prices climbing together, offsets below, later vintages at a premium). Anchor every bot's
reservation price on one expected-price estimate (last auction clearing + trend), so exchange averages
stay within roughly 10-15% of the same year's auction price. Add an envelope for it.

## J3 Un-skip the remaining skipped envelopes
Three stay skipped. Deliver them rather than documenting them:
 - compliance (J1);
 - offsets surrendered about 2-3% of cap (through J1's offsets supply);
 - regulator shock response: a cap cut or floor raise must move exchange prices within one virtual
   month (about 1m40s of a 20-minute year). Definition of done says a trainer pauses and shocks the
   market and the room sees it, so this must work: implement bot reaction to shocks, add the envelope.

## J4 Housekeeping
- The mean-preserving allocation factor (1 in 10 at 1.5, rest 0.9444) is acceptable as original fiction,
  but confirm sector cakes still equal the deck's sector shares and total free allocation is still
  exactly 90% of the cap, and say so in the scenario notes.
- Report the final measured table again (same fields as last time) after calibration, Normal and Hard.

## How to work
Red/green, no commits, keep :5003 serving, report back with the usual command and the measured table.
Do not trade determinism for the fit: seeded runs must still reproduce.
