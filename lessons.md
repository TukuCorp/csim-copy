# lessons

Corrections from the user, and the rule that stops each one recurring.

## 1. Research material is a specification, never content to ship (2026-09-16)

**What happened.** The first scenario file was seeded with 37 company names copied verbatim out of
the harvested `sim3_home.html` login-page dropdown, on the reading that the build plan's phrase
"seeded with the 39 firm names" authorised using them. The user corrected it: nothing in the
scenario may be copied from carbonsim.org, because the original is proprietary and the clone is
built from public documentation and observed behaviour only.

**Why the reading was wrong.** "Seeded with the 39 firm names" was a statement about the *shape*
and *size* of the dataset (39 participating companies of the same kinds: thermal plants, power and
gas utilities, cement, steel), not permission to lift the strings. `research/` is a specification
reference, and the working rules already said not to paste EDF's wording; the same logic applies to
any string that ships, not only UI copy.

**Rule.** Anything that ends up in a shipped artifact — names, wording, parameters presented as
data, sample values — must be written for this project. `research/` may be read to learn structure,
counts, ranges, field names and mechanics; it may never supply the literal value. When a plan item
names a source ("firm names from the login page"), treat it as "the same kind and count", then
author the content, and say in the file's own notes that it is original fiction. Scenario data files
carry a `notes` field for exactly this: state what is derived from documentation and what is
invented.

## 2. A year that has run out must not be resumable, and nothing but the trainer begins a year

**What happened.** QA opened the seeded demo exercise, pressed the "Let the clock run" control, and
found a run that read Running while the year stayed at 1, the auction at 4 / 4 and every countdown
at 0m 00s, for good. The seeded run had been started by the demo seeder at boot, so by the time
anybody looked the twenty-minute year had already run out and the clock had halted itself; the
console then offered the one control that could not help, because resuming a clock whose year is
over left it Running with no time left and no way to close the year.

**Why the reading was wrong.** "A live game to look at" was taken as "start the year for the
trainer". The clock is the administrator's instrument here: a year begins when they begin it, and a
year that has used its time is over rather than paused. A run that starts itself silently spends
the window the trainer is about to talk over, and the only signal left is a countdown already at
zero.

**Rule.** Only the trainer's own control begins a year, and a run on the shelf is seeded waiting for
it. A clock whose year is over refuses a resume and says why; the console shows the year end
instead. Any state a control cannot act on must be reported as such rather than offered: before
adding a control, check the state the engine will accept it in.

The same rule covers the seeding itself: demo content arrives on the shelf, waiting for the trainer,
and boot never advances the clock. A convenience that starts the game for the trainer spends the
window the trainer is about to talk over.

## 3. A position counts what the company holds, and one sign convention runs through every screen

**What happened.** QA bought 256,000 tonnes at auction, watched the holdings table show them, and
still read "Short 255,025" on the top bar, the dashboard, the surrender screen and the admin
company report - the figure was the forecast (emissions less the free allocation) and never moved
however much the company bought. The leaderboard's final-position column had the mirror problem:
it showed what a company was left holding, so a company that owed 255,000 tonnes read as long by
2.3 million. The engine had two meanings of "position" - a shortfall (positive is short) and a
holding (positive is long) - and the screens passed both to one formatter.

**Why the reading was wrong.** "Position" on a screen a player acts from has to be the position as
it stands: what the obligation is less everything the company already holds that it could
surrender, which is the same set year-end reconciliation draws on. The forecast is a different
figure and deserves its own name. Two sign conventions in one engine is the kind of thing that
survives review because each half looks right on its own.

**Rule.** A position counts the instruments the company holds, and positive always means short -
the same way up as the formatter that writes "Short"/"Long". Keep the forecast under a name that
says forecast. When a figure appears on more than one screen, the engine owns one definition of it
and the screens read that one.

## 4. Liquidity claims are measurements, not readings of the code

**What happened.** QA found an empty exchange book four minutes into year 1 and asked whether the
bots trade at all. Reading the bot code said yes - it places limit orders - but the measurement
said something the code did not: the whole fleet is short every year in this scenario, so no bot
ever has a surplus to offer, and the exchange's bid side is the only side that ever appears. The
bid depth is real (242 orders resting by the end of year 1) and a player can sell into it; the
bots simply never meet each other.

**Rule.** When a question is about market behaviour, answer it with a measurement over a seeded run
and report the numbers per year, including the ones that show nothing happening. An empty book at
minute four and a one-sided book all year are different findings, and only the measurement tells
them apart.

## 5. A market needs somebody with a surplus, and the scenario is where that comes from

**What happened.** Round 2 measured a book with bids and no offers and reported it as a property of
the scenario. That was true but it was not the end of the question: the free allocation was a flat
90% of a cap the fleet's emissions exactly met, so every unit was short by the same fraction of its
baseline and no bot could ever hold anything to sell. A market with no sell side is not a market, and
no amount of bot cleverness fixes it - only the distribution of allowances does.

**Why the reading was wrong.** "The fleet is short" was treated as a fixed fact about the exercise
rather than as a consequence of how the allocation was split. The clone's own rules already had the
lever: a unit's share of its sector's cake is a weight, and a weight that is not proportional to
current emissions is what grandfathering, benchmarking and every real allocation dispute are about.

**Rule.** When a market in the simulation has no side, look for the distribution that would give it
one before tuning the agents: even a mean-preserving spread between companies (some allocated above
their need, some below) makes sellers without changing the cap, the cake or the aggregate
obligation. Change the scenario for it, keep the weights mean-preserving, and write down in the
scenario notes that the pattern is fiction and why it is there.

## 6. Measure every lever before declaring two envelopes mutually exclusive (2026-10-10)

**What happened.** QA asked for the compliance-versus-price frontier, and the first answer swept the
offset supply and the bot abatement margin and reported that the two documented targets could not
both hold: the compliance band wanted more cheap allowances than the price band would tolerate. QA
pushed back, because the sweep had never touched the abatement menu. Re-authoring the menus so each
sector's rungs cost about 5, 130 and 240 a tonne, with the middle rung economic only once the market
tightens, took the widest-offsets row to 527 of 726 compliant company-years with just 17 units
penalised in year 2, and left the price path inside its band (100.00 / 100.00 / 234.58) without
changing a single offset.

**Why the reading was wrong.** "Mutually exclusive" was concluded from two levers of the four a bot's
decision actually reads. The abatement menu and the allocation spread were held fixed, and the
un-swept one was the lever that mattered. An impossibility claim is only as good as the space that
was searched, and the space here was two dimensions wide.

**Rule.** Do not say two targets cannot both hold until every lever that feeds the decision has been
swept, and say which levers and which ranges in the answer, including the ones still held fixed.
When the answer is "no point exists", publish the grid that shows it, so the claim can be re-measured
instead of trusted.

## 7. A clipped one-pixel label can still widen the page (2026-10-11)

**What happened.** The console's setup screen gained screen-reader-only labels in the QA loop. They
were positioned absolutely with no positioned ancestor, so their containing block was the page itself:
they sat at their static position inside a 936px-wide abatement table but escaped the card's
overflow-x: auto and widened the document by 429px at phone width, while being one pixel and clipped.
The phone-width gate caught it only sometimes, because a measurement taken before the tables rendered
saw nothing wrong, so it read as a flake for two rounds before the browser was asked which element was
wide.

**Why the reading was wrong.** "One pixel, clipped, invisible" was taken to mean "cannot affect
layout". Absolute positioning takes an element out of its ancestors' overflow clipping whenever none of
those ancestors is positioned, so a one-pixel label inside a wide table can still push the page
sideways.

**Rule.** Pin a screen-reader-only element (left: 0; top: 0) or make its container a containing block,
so it can never contribute to the page's scrollable overflow. And when a phone-width gate fails
intermittently, measure which element is wide before calling it a flake.


## 8. A sign convention change needs a test on both sides of the sign (2026-10-11)

**What happened.** The Standing change moved the player view's position figure onto the new module and,
in doing so, changed which way up it was. The test that shipped with it asserted only a LONG company
(one holding more than it emits); a SHORT company was never rendered. Display.Position(PositionView)
went on feeding the long-positive figure to the short-positive formatter, so every short company read
"Long N" on the top bar, the dashboard and the surrender screen. The suites stayed green because
nothing asserted the short case; a live check found it.

**Why the reading was wrong.** "The long case passes and the numbers did not move" was taken as enough.
A sign is the one thing a single-sided test cannot pin: with the wrong sign the opposite case reads
correctly, which is exactly what a test that covers one side will accept.

**Rule.** When a change touches a sign convention - shortfall against position, held against available,
debit against credit - test BOTH sides of the sign in the same change, plus the zero or settled case,
and check the screen it lands on live. A one-sided test on a sign is not a test.

## 9. A refactor that changes a definition must list the screen numbers it moves (2026-10-11)

**What happened.** Standing redefined "held" to count escrowed allowances, where the module it replaced
counted only what was available. The change is right, but it moves numbers a player reads. The pass
carried it through the engine and the tests without listing, screen by screen, which figure changed and
from what to what, and without a live look at the screens themselves; the label inversion it caused was
found only by hand.

**Why the reading was wrong.** "The tests are green and the golden is byte-identical" answers the
engine, not the screens. A definition swap can leave every engine test passing while a screen shows a
different number, because the screen reads the module and not the assertion.

**Rule.** When a refactor swaps a definition (held now counts escrow; the shortfall is now clamped; the
forecast is renamed), enumerate every screen figure that changes and give a before and after for each,
and check the screens live, not only the suites. Report the moved numbers as changes rather than as a
green suite.

## 10. Never report green, or that a test bites, without running the suite after the last edit (2026-10-11)

**What happened.** Three reports in this pass were wrong for the same reason: a suite result, or a "the
test bites" claim, was reported from a run taken before the last edit, so it described a tree that no
longer existed. The correction in each case was a re-run, not a different result.

**Why the reading was wrong.** "It was green a moment ago and I only touched X" is a guess about what
the suite covers. A later edit - even a comment or a test-only file - can change what runs, and an
earlier run is not evidence for the tree as it stands.

**Rule.** Run the suite, and re-run the bite proof, AFTER the last edit, and quote that run. If the last
edit came after the run, the claim is not evidence; give the counts from the run that followed the final
change.

## 11. Size the delivery route to the target, not to the artifacts already in the repo (2026-10-17)

**What happened.** The internal-demo gap analysis rated the broken Dockerfile CRITICAL and put it on
day one of the sprint, because a Dockerfile existed and was broken. The user asked "why we need
docker" and then removed it from the stack: an internal demo on a box we control needs a published
build, a launcher and a tunnel, and nothing a container adds.

**Why the reading was wrong.** "There is a deployment artifact and it is broken" was read as "the
target depends on it". The target - colleagues reaching one instance in their browsers - was never
checked against what the container actually buys (portability to hosts without .NET, image
reproducibility), none of which an internal session needs.

**Rule.** In a gap analysis or plan, rate an existing artifact's defect by whether the stated target
needs that artifact, and name the simplest route that meets the target first. If an artifact is only
needed on some routes, say so and rate it for the recommended route, not the worst case.
