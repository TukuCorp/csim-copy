using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Market;

/// <summary>
/// Nothing may be created or destroyed between the cap and the companies: every tonne of a
/// vintage is either handed out free, sitting in the government's reserve, or waiting in an
/// auction that has not cleared yet.
/// </summary>
public sealed class AllowanceConservationTests
{
    private static decimal InCirculation(Simulation simulation, AuctionSchedule schedule, int vintage)
    {
        Product product = Product.Allowance(vintage);
        decimal atCompanies = simulation.Companies.Sum(company => simulation.Ledger.Held(company, product));
        decimal inOpenAuctions = schedule.Auctions
            .Where(auction => !auction.IsCleared && auction.Lots.Any(lot => lot.Vintage == vintage))
            .Sum(auction => auction.Lots.Where(lot => lot.Vintage == vintage).Sum(lot => lot.Volume));

        return atCompanies + simulation.Government.Held(vintage) + inOpenAuctions;
    }

    [Fact]
    public void Every_tonne_of_the_cap_is_handed_out_reserved_or_on_offer()
    {
        Simulation simulation = TestSimulation.Build();
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);

        foreach (int year in simulation.Allocation.Years)
        {
            // What the run does at the start of a year: hand out that year's free allocation.
            simulation.Ledger.GrantFreeAllocation(year);

            InCirculation(simulation, schedule, year).Should().Be(
                simulation.Allocation.CapForYear(year),
                $"vintage {year} must account for its whole cap");
        }
    }

    [Fact]
    public void Clearing_an_auction_moves_volume_without_losing_any()
    {
        Simulation simulation = TestSimulation.Build();
        simulation.Ledger.GrantFreeAllocation(1);
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);
        Auction auction = schedule.ForSection(1, 1);
        auction.PlaceBid(simulation.FindUnit(1), 1, 150m, 100_000m);

        auction.Clear();

        auction.UnsoldVolume.Should().Be(0m, "unsold lots go back to the reserve");
        InCirculation(simulation, schedule, 1).Should().Be(simulation.Allocation.CapForYear(1));
        simulation.Government.Held(1).Should().Be(
            237_500m - 100_000m,
            "the part of this auction nobody bought is back in the reserve; the year's other three auctions are still holding theirs");
    }

    [Fact]
    public void An_auction_reports_what_it_is_offering_and_what_is_left_of_it()
    {
        Simulation simulation = TestSimulation.Build();
        AuctionSchedule schedule = AuctionSchedule.Build(simulation, forwardVintageShare: 0.20m);
        Auction auction = schedule.ForSection(1, 4);

        auction.OfferedVolume.Should().Be(237_500m + 184_300m);
        auction.UnsoldVolume.Should().Be(auction.OfferedVolume);

        auction.Clear();

        auction.UnsoldVolume.Should().Be(0m);
    }
}
