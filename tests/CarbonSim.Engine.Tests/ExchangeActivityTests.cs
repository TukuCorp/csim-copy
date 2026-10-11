using System.Globalization;
using System.Text;
using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Scenarios;
using FluentAssertions;
using Xunit.Abstractions;

namespace CarbonSim.Engine.Tests;

/// <summary>
/// G3: whether the bots put anything on the exchange's books while a year is being played. The
/// demo run's book was empty four minutes into year 1, which turns out to be a question of when
/// the bots trade rather than whether they do: a bot's trade trigger is drawn between 40% and 60%
/// of the year, so nothing has happened by minute 4 of 20 and the book fills from minute 8.
/// </summary>
public sealed class ExchangeActivityTests
{
    private readonly ITestOutputHelper _output;

    public ExchangeActivityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Bots_rest_and_fill_orders_on_the_exchange_once_the_year_is_half_played()
    {
        List<YearActivity> years = Play(TimeSpan.FromSeconds(15));
        YearActivity first = years[0];

        _output.WriteLine(Report(years));

        // QA's minute 4: the fleet's orders are already in the book, on both sides, because the
        // trade trigger is drawn in the first sixth of the year rather than after 40% of it.
        first.RestingVolumeAt(0.20m).Should().BeGreaterThan(0m, "the fleet trades inside the first auction cycle");
        first.BidsAt(0.20m).Should().BePositive("the short units bid for what they still need");
        first.AsksAt(0.20m).Should().BePositive("the units allocated above their need offer the surplus");
        first.FirstOrderAt.Should().BeLessThan(0.20m, "a trade trigger is drawn between 5% and 15% of the year");

        // And the two sides meet, so the exchange actually trades.
        first.Fills.Should().BePositive("offers and bids cross");
        first.Volume.Should().BePositive();
        first.RestingVolumeAt(0.65m).Should().BeGreaterThan(0m);
    }

    /// <summary>Plays the shipped scenario, one step at a time, recording what the books held.</summary>
    private static List<YearActivity> Play(TimeSpan step)
    {
        Simulation simulation = ScenarioLoader.LoadFile(Path.Combine("scenarios", "vietnam-2024.json"));
        Parameters parameters = simulation.Rules;
        TestTimeProvider time = new();
        SimulationClock clock = new(simulation, time);

        foreach (Unit unit in simulation.Units)
        {
            unit.AutoTrade = true;
        }

        SimulationBots bots = SimulationBots.Create(simulation);
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);
        Exchange exchange = new(simulation);
        BotMarkets markets = new(exchange, new OtcMarket(simulation), schedule);
        List<YearActivity> years = [];

        clock.Start();

        for (int year = 1; year <= parameters.Years; year++)
        {
            simulation.Ledger.GrantFreeAllocation(year);

            YearActivity activity = new(year, parameters.YearLength);
            long seen = 0;
            years.Add(activity);

            while (clock.State != SimulationState.YearEnded)
            {
                time.Advance(step);
                decimal elapsed = (decimal)clock.ElapsedInYear.TotalMinutes / (decimal)clock.YearLength.TotalMinutes;

                foreach (ClockEvent closed in clock.Advance().Where(@event => @event.Kind == ClockEventKind.AuctionClosed))
                {
                    schedule.ForSection(closed.Year, closed.Auction).Clear();
                }

                foreach (BotAction action in bots.Act(simulation, clock, markets))
                {
                    activity.Record(action);
                }

                if (clock.State == SimulationState.Running)
                {
                    activity.Sample(elapsed, Resting(exchange));
                }

                IReadOnlyList<MarketTrade> trades = simulation.Journal.Trades;

                for (long index = seen; index < trades.Count; index++)
                {
                    if (trades[(int)index].Channel == TradeChannel.Exchange)
                    {
                        activity.Filled(trades[(int)index]);
                    }
                }

                seen = trades.Count;

                if (clock.State == SimulationState.TradingHalted)
                {
                    clock.EndYear();
                }
            }

            simulation.Compliance.Reconcile(year);
            simulation.Finance.CloseYear(year);

            if (year < parameters.Years)
            {
                clock.BeginNextYear();
            }
        }

        return years;
    }

    /// <summary>Every resting order across every book: how much, how many, and on which side.</summary>
    private static (decimal Volume, int Orders, int Bids, int Asks) Resting(Exchange exchange)
    {
        decimal volume = 0m;
        int orders = 0;
        int bids = 0;
        int asks = 0;

        foreach (Product product in exchange.Products)
        {
            foreach (Order order in exchange.Book(product).OpenOrders)
            {
                volume += order.Volume - order.FilledVolume;
                orders++;

                if (order.Side == OrderSide.Buy)
                {
                    bids++;
                }
                else
                {
                    asks++;
                }
            }
        }

        return (volume, orders, bids, asks);
    }

    private static string Report(List<YearActivity> years)
    {
        StringBuilder text = new();
        text.AppendLine("Exchange activity, seeded 242-unit run, 15s steps:");
        text.AppendLine("| Year | Orders at 20% | Orders at 100% | Bids / asks at 100% | Resting volume at 50% | Resting volume at 100% | Fills | Volume traded | First order at |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|");

        foreach (YearActivity year in years)
        {
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"| {year.Year} | {year.OrdersAt(0.20m)} | {year.OrdersAt(1.00m)} | {year.BidsAt(1.00m)} / {year.AsksAt(1.00m)} | {year.RestingVolumeAt(0.50m):N0} | {year.RestingVolumeAt(1.00m):N0} | {year.Fills} | {year.Volume:N0} | {(year.FirstOrderAt is { } at ? at.ToString("P0", CultureInfo.InvariantCulture) : "never")} |");

            text.AppendLine(CultureInfo.InvariantCulture, $"  Year {year.Year}: {year.Actions} bot actions, {year.Refusals} refused");

            foreach (string reason in year.RefusalReasons)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"    {reason}");
            }
        }

        return text.ToString();
    }

    /// <summary>One virtual year's exchange activity, sampled as the year is played.</summary>
    private sealed class YearActivity
    {
        private readonly List<(decimal At, decimal Volume, int Orders, int Bids, int Asks)> _samples = [];

        public YearActivity(int year, TimeSpan length)
        {
            Year = year;
            Length = length;
        }

        public int Year { get; }

        public TimeSpan Length { get; }

        public int Fills { get; private set; }

        public decimal Volume { get; private set; }

        /// <summary>The most orders that rested at any one moment in the year.</summary>
        public int Orders => _samples.Select(sample => sample.Orders).DefaultIfEmpty(0).Max();

        public decimal? FirstOrderAt => _samples.FirstOrDefault(sample => sample.Orders > 0) is { Orders: > 0 } first
            ? first.At
            : null;

        public void Sample(decimal at, (decimal Volume, int Orders, int Bids, int Asks) resting)
        {
            _samples.Add((at, resting.Volume, resting.Orders, resting.Bids, resting.Asks));
        }

        /// <summary>
        /// What the fleet's bots decided, and in particular what the market refused: a bot that
        /// wanted to offer and could not says so here rather than being invisible in the book.
        /// </summary>
        public void Record(BotAction action)
        {
            Actions++;

            if (action.Detail.Contains("could not", StringComparison.Ordinal)
                || action.Detail.Contains("refused", StringComparison.Ordinal))
            {
                Refusals++;
                _refused.TryGetValue(action.Detail, out int seen);
                _refused[action.Detail] = seen + 1;
            }
        }

        public int Actions { get; private set; }

        public int Refusals { get; private set; }

        public IReadOnlyList<string> RefusalReasons =>
        [
            .. _refused.OrderByDescending(pair => pair.Value).Take(3).Select(pair => $"{pair.Value} x {pair.Key}"),
        ];

        private readonly Dictionary<string, int> _refused = [];

        public void Filled(MarketTrade trade)
        {
            Fills++;
            Volume += trade.Volume;
        }

        /// <summary>The resting volume at the sample nearest a fraction of the year.</summary>
        public decimal RestingVolumeAt(decimal fraction) => _samples
            .Where(sample => sample.At <= fraction)
            .Select(sample => sample.Volume)
            .DefaultIfEmpty(0m)
            .Max();

        public int OrdersAt(decimal fraction) => _samples
            .Where(sample => sample.At <= fraction)
            .Select(sample => sample.Orders)
            .DefaultIfEmpty(0)
            .Max();

        public int BidsAt(decimal fraction) => _samples
            .Where(sample => sample.At <= fraction)
            .Select(sample => sample.Bids)
            .DefaultIfEmpty(0)
            .Max();

        public int AsksAt(decimal fraction) => _samples
            .Where(sample => sample.At <= fraction)
            .Select(sample => sample.Asks)
            .DefaultIfEmpty(0)
            .Max();
    }
}
