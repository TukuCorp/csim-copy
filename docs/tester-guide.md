# Tester guide - the internal CarbonSim demo

You are testing our clone of a carbon-market training game. Each player runs a company that emits
CO2 and has to cover its emissions each virtual year with allowances (bought at auction, on the
exchange or from another company) or by cutting emissions. Around 200 AI companies trade alongside
you. One person, the trainer, runs the clock from a console.

We want to know where the game breaks, where a screen confused you, and where a number looks
wrong. Read section 6 before you file anything: some behaviour that looks odd is deliberate.

## 1. Getting in

The trainer sends you three things: the address, the registration PIN, and the time the session
starts.

1. Open the address in Chrome, Edge or Firefox. A phone works, but a laptop is easier.
2. Under **Register and claim a company**, enter your email, a display name, a password and the
   PIN. Pick a company from the list. Each company can be claimed once, and an account keeps its
   company for good.
3. You land on the waiting screen. Once the trainer has created the exercise, a button takes you
   into it.

The switch at the top of every page changes the language between English and Tiếng Việt. Try both
if you read Vietnamese.

## 2. How a year runs

A virtual year lasts about 20 minutes of real time (the trainer can shorten it). It has four
sections, one quarter of the virtual calendar each. Each section opens with a sealed-bid auction
window. The top bar shows the virtual date; the progress strip under it shows the year, the auction
and the countdowns, and counts down to a halt when the trainer gives warning. At the end of the year the trainer halts trading
and closes the year. That is when your company surrenders allowances against its emissions, and
any shortfall is charged a penalty.

The top bar is the summary to watch: cash, your position (long means you hold more than you will
emit, short means you need more), and the best bid, offer and last price.

## 3. A ten-minute tour

| Screen | What to look at |
|---|---|
| Dashboard | Your finance and compliance, the long/short bar, auction and trade history |
| Company management, Unit information | What you own and what each unit emits |
| Abatement | The cost curve: projects that cut emissions, cheapest first, with their cost per tonne |
| Allowance auction | Bid a volume and a price for a vintage while the window is open |
| Exchange market | The price chart, the top of the book, and the order form |
| OTC market | Offer allowances to a named company, and accept or reject offers sent to you |
| Surrender and banking | What you will hand in at year end and what carries over |
| System info, Leaderboard | Market-wide totals, and how your cost of compliance ranks |
| Messages | The room's chat and notices from the trainer |

## 4. Things to try as a player

Work through these over the session. Note anything that surprised you.

- [ ] Read your position on the top bar before the first auction, then again after it.
- [ ] Place an auction bid while the window is open. Place another after it has closed (it should be refused, with a reason).
- [ ] After an auction clears, find what you won and what you paid.
- [ ] Put a limit order on the exchange that rests in the book. Cancel it.
- [ ] Put an order on the exchange that fills, and find the trade in your history.
- [ ] Send an OTC offer to another player's company and have them accept or reject it.
- [ ] Implement an abatement project and watch it move from building to operating over the years.
- [ ] Check the surrender screen just before the year closes and again just after.
- [ ] Switch to Tiếng Việt and type a price the Vietnamese way (for example 120,25).
- [ ] Open the game on a phone and see whether any screen is unusable.

## 5. If you are the trainer

Sign in with the trainer account the operator gives you, then open the console at /admin.

1. **New exercise.** Check the rules. For a one-hour session set the year length to 10 minutes.
   Pick a seed and write it down, then press **Create the exercise**.
2. Open the exercise and press **Start the year**. Let the room work through the auctions.
3. Try the controls: **Pause the clock**, **Let the clock run**, send a fine, disburse offsets to a
   unit, and move the price collar on the "Change the rules" card. Check that the players see each
   one.
4. At the end of the year press **Halt trading**, then **Close the year**, then **Open the next
   year**.
5. On the reports page, open each report and download its CSV.

## 6. Known limits (deliberate, please do not file these)

- **Some companies end up short in year 3.** With the shipped calibration about 67% of
  company-years comply (485 of 726 in the seeded run), and most penalties fall in the last year.
  The cap falls 3% a year while emissions grow. This is a calibration choice we have measured and
  documented, not a bug in the settlement.
- **The exchange has few sellers.** Most companies are short every year, so only a couple of dozen
  AI companies ever offer allowances. Expect plenty of bids and a thin offer side.
- **A mid-run change moves only the price collar and the market switches.** The trainer can move
  the floor and ceiling, and open or close the exchange, the OTC market and the leaderboard, during
  a year. The cap and allocation are fixed when the exercise is built.
- **A restart goes back to the start of the year.** The server saves an exercise when it is created
  and at each year end. If the host goes down mid-year, the trainer loads the exercise back from
  the last year end and that year is played again.
- **AI companies wait for the first auction before quoting.** They first trade 12-20% of the way
  into the year, just after the year's first auction has cleared, so the exchange book is empty
  for the first few minutes.
- **A halt cancels resting orders and OTC offers.** When the trainer halts trading, your open
  orders and unanswered offers are cancelled and what they had set aside comes back to you.

## 7. Reporting a problem

Every page has a line at the bottom like `Build 1.0.0+abc1234   Run 1a2b3c4d   Seed 20240916`.
Copy it into every report. It tells us exactly which code and which run you were on.

File reports as GitHub issues in TukuCorp/csim-copy using the **Demo feedback** template. It asks
for the stamp, the screen, the time and virtual year, what you did, and what you expected. A
screenshot helps. If you cannot use GitHub, send the same fields to the trainer.

## 8. If something goes wrong

- **A red bar at the bottom of the page** means that page lost its link to the server. Press
  Reload. Your company's state is kept on the server, so nothing is lost.
- **"Reconnecting" over the page** means the network dropped. It usually recovers on its own. If it
  does not, reload.
- **You forgot your password.** Use **Forgot your password?** on the sign-in page and press "Send
  me a code". The demo sends no email, so ask the operator for the code (it is in the host log),
  then enter it there with your new password.
- **Everything stopped for everyone.** The host went down. Wait for the operator to restart it and
  the trainer to load the exercise back.

## 9. Before the session (operator's checklist)

Run this once on the demo box, with two or three colleagues, before the real session.

- [ ] Start the host with `powershell -File scripts/run-demo.ps1` (see docs/deployment.md) and open
      the tunnel or LAN address from another machine.
- [ ] Register two players from two different machines, and sign the trainer in from a third.
- [ ] Play all three years with short years (5 minutes), through to the leaderboard and the CSV
      reports.
- [ ] Mid-session, stop the host with Ctrl+C, start it again with `-NoPublish`, load the exercise
      from **Saved exercises**, and carry on.
- [ ] Leave one player's page idle for 10 minutes and check it still updates (tunnels can drop idle
      connections).
- [ ] Copy demo-data/carbonsim.sqlite and demo-data/logs/ into reports/qa/ afterwards, and note what
      went wrong in reports/qa/feedback-08.md.
