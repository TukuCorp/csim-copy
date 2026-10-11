# QA feedback 04b (small live findings, from Claude; do after round 4)

Round 3 UI verified live: H1 (no countdown after year end), H2 (year 2 cap 345,174,500 / to date 701,024,500),
H5 (forms preselect, success line shown), early bot quotes (bid 96 / offer 104 visible ~2.5 min in),
market buy of 5,000 t filled at 104 and capital/position updated correctly. Remaining issues:

## K1 Exchange "Top of book" is wrong
After the market buy, Best offer is still 104.00 USD (strip and top bar agree) but the SELLING column of
Top of book is "-" on every row, and the BUYING column lists one row per order, e.g.
"45,467 @ 99.84 / 45,467 @ 99.84 / 45,467 @ 99.84 / 45,467 @ 96.00 ...". Aggregate by price level
(sum volume per price, best first, 5 levels per side) and show the asks in the SELLING column.

## K2 No visible confirmation after Place order
The order or fill is reflected in My orders / Last trades but nothing says "Bought 5,000 at 104.00".
Show a one-line result next to the button (filled / partly filled / resting / rejected + reason).

## K3 Closing or halting during an open auction discards it silently
I closed the year ~4-5 minutes in, while auction 1 (window 2:45-5:00) was open. Afterwards My auction
history shows auction 1 with Cleared at "-" and 0 awarded, system auction revenue 0, and then 218 units
penalised for 14.4 bn. Decide and implement one behaviour: either the close clears the open auction
first (preferred: bids placed are honoured), or the console warns "an auction is open - close year
anyway?" and the unresolved bids are released, with a message to players. Add a test.

## K4 Report row label
System report "Average Allowance auction 104.00 USD" appeared after an exchange trade at 104 while
"Auction revenue" was 0, so that row mixes exchange trades into an auction-labelled average. Label it
for what it is (average allowance price, all channels) and show the auction average separately.
