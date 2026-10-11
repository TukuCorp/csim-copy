# Architecture pass, round D (from Claude): privacy check on C1, then C3 (Standing)

C1 and C2 accepted. Thank you for saying C3 was not started.

## D0 (check first, possible confidentiality bug) Are released-bid notices private?
You wrote that each released bid writes a notice "into the feed ... one feed every player reads". Auction bids
are sealed: price and volume of one company's bid must never be visible to other companies. Verify, with a test
at the host level (two player sessions, two companies):
 - a released-bid notice reaches ONLY the owning company's messages (and the trainer's feed), not the other company;
 - the same for released exchange orders if their notices name the owner's order;
 - the public rule-change line says only counts ("2 orders and 3 auction bids released"), never volumes, prices or names.
If the feed is global today, fix it: notices have an audience (all players, one company, trainer only) and the
released-bid notice is addressed to its company. Language: a private notice can use its recipient's culture if the
host knows it, otherwise keep the en + vi joined text. Red test first.

## D1 C3 as specified in reports/arch/refactor-03.md (section C3), unchanged
Standing module, six callers moved, register company-year lookup, one offset-limit computation, bot pricing-only
estimate named, SystemReport sold == bought question resolved, table-driven Engine test, player-view integration
test, replace the console position/system report assertions Standing now covers (list them). Golden byte-identical
or each deliberate difference listed; screens whose number changes because they used a different rule before are
bug fixes: list each with a before/after example. Acceptance 1-5 from that section apply.

Rules unchanged: red/green, NO commits, keep :5003 serving. Report with the usual command.
