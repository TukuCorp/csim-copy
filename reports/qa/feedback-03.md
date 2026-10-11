# QA feedback 03 (from Claude). Round 2 verified live: G1 (Long 44,975 after winning 300,000 vs short 255,025),
G2 (result card with My won / My paid), G4 (switch visible, keeps the page), G6/G7 visuals all confirmed.
Year-end reconcile also looks right: surrendered 2,550,258, banked 44,975, year 2 free allocation
2,226,376 (-3%), allowances held 2,271,351 (banked carried). Screenshots: reports/qa/r2-*.png.

## H1 (bug) Position/progress still stale after the year is closed
After "Close the year" the player's Surrender screen and top bar show "Current position Short 2,505,283"
(allowances held 44,975 vs forecast emissions 2,550,258) although the year is settled and shortfall = 0.
After reconciliation show the settled outcome (Compliant / banked 44,975), not a fresh short position.
Also while "Year ended" the progress strip still reads "Allowance auction starts in 1m 47s", and the admin
console still shows TIME LEFT 14m 02s / NEXT AUCTION OPENS IN 1m 47s. Clear or hide those countdowns
when the year has ended.

## H2 (bug) System report "THIS YEAR" / "TO DATE" columns are wrong for cap-type rows
In year 2 (Running, auction 1) the admin and player System report show:
  Cap 701,024,500 (this year) / 1,035,843,765 (to date); Free allocation 630,922,050 / 932,259,389;
  Allowances on offer 70,102,450 / 103,584,376.
Year-2 cap should be 345,174,500 (355.85M x 0.97), free allocation ~310.6M, on offer ~34.5M.
"This year" is showing years 1+2 combined and "to date" is showing all three years of the simulation.
Expected: this year = the current year only; to date = years elapsed so far (including this one).
Forecast emissions are right (367.5M this year / 723.4M to date), so the cap/allocation/offer rows use a
different aggregation. Add a red test with a 2-year engine run.

## H3 (fidelity, important) Bots never post offers, and only start resting bids at ~41% of the year
Thanks for the measurement. I do not accept "scenario has everyone short" as the end of it: the
acceptance script (video section 8, bot benchmark in section 1) needs a two-sided exchange: best
offer, trades that fill against depth, offsets at a discount, and the lecture's bot benchmark of overall
MCC between about +$31 and -$40/t. A book with only bids and no asks means a human buyer can never
buy on the exchange and nothing ever trades. Please:
 1. Find what makes real two-way depth in the original design and build it: bots that hold surplus
    (won more at auction than needed, banked allowances, offsets handed out by the admin, completed
    abatements that make them long) should post offers above their cost, and short bots should
    bid below the auction ceiling; offsets should be offered at a discount (3-5%).
 2. Start resting orders earlier in the year (bots should be visible within the first auction cycle, not
    only after 40%), while keeping the seeded determinism.
 3. Measure with a full seeded 3-year run (engine/driver, fast clock) and report per year: orders
    by side, fills, volume, average price vs auction clearing price, offsets discount, number of units
    penalised, overall MCC range and the leaderboard spread, against the section 6 envelopes. Un-skip
    the 4 skipped fidelity envelopes if they can now pass; if one cannot, say why.
If this needs a scenario change, change it (document in the scenario notes as original fiction) rather
than leaving the market one-sided; run the fidelity suite after.

## H4 (check) Compliance after a short year looked catastrophic
I closed year 1 about 4 minutes in (only auction 1 had run). The admin System report then showed
"Penalty per tonne 241 / 8,814,621,900 USD", i.e. 241 of 242 units penalised. That is expected for an
early close, but please verify with the full-year run from H3 that in a normal year most bots surrender
fully (target from the briefing: bots comply at least cost), and report the penalised count per year.
Also, is it right that penalties are computed on the full shortfall of units that simply were not
given time to trade? An early close is a legitimate trainer action (end-of-year, pause, end sim).

## H5 (bug) Admin action forms fail silently
In the admin run console the "Fine a unit", "Hand out offsets", "Add abatement for a unit" and
"End-of-year modification" selects start blank (no option pre-selected, screenshots r2-admin-forms.png).
Pressing the button with a blank unit does nothing: no error, no success message. A trainer cannot tell if the
action worked. Fix: (a) validation message when a required field is missing; (b) success line after each
action ("Fined X 500,000 USD", "Handed out 100,000 offsets to Y") and an engine error shown on failure;
(c) the select <label>s are not programmatically associated with their <select> (select.labels is empty),
fix for accessibility. (d) The "Player messaging ON | Off" toggle highlights "Off" while the player
Messages page still allows sending; make the control state unambiguous (show "Messaging: on/off" text plus a
single toggle). Add a Playwright test that performs each action end to end and checks the effect on the
player's screen (capital, holdings, messages).

## H6 Small
- OTC counterparty list: 241 flat entries sorted AI Cement 01..; humans are buried. Put human
  companies first, then AI by sector, and add a type-to-filter.
- Language links are blue on the navy top bar (low contrast, ~3:1); use light text on dark.
- "Hand out offsets" label says Company while the other forms say Unit; make consistent.

## How to work
Same rules as before: red/green, no commits, keep :5003 serving, report back with the usual command.
Order: H2 (quick, engine), H1, H5, then H3 (largest; report the measured numbers) and H4 from the same run.
