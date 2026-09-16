using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Market;

public sealed class AuctionTests
{
    private const decimal LotVolume = 237_500m;

    private static Auction FirstAuction(Simulation simulation)
    {
        return AuctionSchedule.Build(simulation).ForSection(year: 1, sequence: 1);
    }

    [Fact]
    public void The_schedule_offers_the_governments_share_of_each_year_cap_by_vintage()
    {
        Simulation simulation = TestSimulation.Build();
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);

        schedule.Auctions.Should().HaveCount(12);

        Auction first = schedule.ForSection(1, 1);
        first.Year.Should().Be(1);
        first.Sequence.Should().Be(1);
        first.Lots.Should().ContainSingle().Which.Vintage.Should().Be(1);
        first.Lots.Single().Volume.Should().Be(LotVolume);
        first.Lots.Single().IsForward.Should().BeFalse();

        schedule.Auctions.Where(auction => auction.Year == 1).Sum(auction => auction.Lots.Sum(lot => lot.Volume))
            .Should().Be(950_000m, "90% of the 9,500,000 tonne cap is handed out free");
    }

    [Fact]
    public void A_year_whose_auctionable_volume_does_not_divide_evenly_keeps_every_tonne()
    {
        Simulation simulation = TestSimulation.Build();
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);

        Auction last = schedule.ForSection(3, 4);
        Auction firstOfYearThree = schedule.ForSection(3, 1);

        firstOfYearThree.Lots.Single().Volume.Should().Be(223_463m);
        last.Lots.Single().Volume.Should().Be(223_466m);
        schedule.Auctions.Where(auction => auction.Year == 3).Sum(auction => auction.Lots.Sum(lot => lot.Volume))
            .Should().Be(893_855m);
    }

    [Fact]
    public void A_forward_auction_sells_part_of_a_later_vintage_early()
    {
        Simulation simulation = TestSimulation.Build();
        AuctionSchedule schedule = AuctionSchedule.Build(simulation, forwardVintageShare: 0.20m);

        Auction lastOfYearOne = schedule.ForSection(1, 4);
        lastOfYearOne.Lots.Should().HaveCount(2);
        lastOfYearOne.Lots[0].Vintage.Should().Be(1);
        lastOfYearOne.Lots[0].IsForward.Should().BeFalse();
        lastOfYearOne.Lots[1].Vintage.Should().Be(2);
        lastOfYearOne.Lots[1].IsForward.Should().BeTrue();
        lastOfYearOne.Lots[1].Volume.Should().Be(184_300m);

        schedule.ForSection(2, 1).Lots.Single().Volume.Should().Be(184_300m, "the rest of vintage 2 is spread over its own year");
        schedule.Auctions.Where(auction => auction.Lots.Any(lot => lot.Vintage == 2))
            .Sum(auction => auction.Lots.Where(lot => lot.Vintage == 2).Sum(lot => lot.Volume))
            .Should().Be(921_500m);
    }

    [Fact]
    public void Everything_the_government_issued_is_either_scheduled_or_kept_in_reserve()
    {
        Simulation simulation = TestSimulation.Build();

        AuctionSchedule.Build(simulation);

        simulation.Government.Held(1).Should().Be(0m);
        simulation.Government.Held(2).Should().Be(0m);
        simulation.Government.Held(3).Should().Be(0m);
    }

    [Fact]
    public void A_bid_has_to_be_inside_the_price_collar()
    {
        Simulation simulation = TestSimulation.Build();
        Auction auction = FirstAuction(simulation);
        Unit unit = simulation.FindUnit(1);

        Action belowFloor = () => auction.PlaceBid(unit, 1, 99m, 1_000m);
        Action aboveCeiling = () => auction.PlaceBid(unit, 1, 301m, 1_000m);

        belowFloor.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*floor*");
        aboveCeiling.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*ceiling*");
        auction.Bids.Should().BeEmpty();
    }

    [Fact]
    public void A_bid_has_to_be_for_a_vintage_on_offer_and_carry_a_volume()
    {
        Simulation simulation = TestSimulation.Build();
        Auction auction = FirstAuction(simulation);
        Unit unit = simulation.FindUnit(1);

        Action wrongVintage = () => auction.PlaceBid(unit, 2, 150m, 1_000m);
        Action noVolume = () => auction.PlaceBid(unit, 1, 150m, 0m);

        wrongVintage.Should().Throw<ArgumentException>().WithMessage("*vintage*");
        noVolume.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*volume*");
    }

    [Fact]
    public void A_unit_may_bid_several_times_and_bids_are_sealed_until_the_auction_clears()
    {
        Simulation simulation = TestSimulation.Build();
        Auction auction = FirstAuction(simulation);
        Unit unit = simulation.FindUnit(1);

        auction.PlaceBid(unit, 1, 150m, 200_000m);
        auction.PlaceBid(unit, 1, 140m, 100_000m);

        auction.Bids.Should().HaveCount(2);
        auction.Bids.Select(bid => bid.Id).Should().Equal(1, 2);
        auction.IsCleared.Should().BeFalse();
    }

    [Fact]
    public void When_demand_exceeds_the_offer_the_auction_clears_at_the_marginal_bid_and_everyone_pays_it()
    {
        Simulation simulation = TestSimulation.Build();
        Auction auction = FirstAuction(simulation);
        Unit first = simulation.FindUnit(1);
        Unit second = simulation.FindUnit(3);
        auction.PlaceBid(first, 1, 150m, 200_000m);
        auction.PlaceBid(second, 1, 140m, 100_000m);

        IReadOnlyList<AuctionResult> results = auction.Clear();

        AuctionResult result = results.Should().ContainSingle().Subject;
        result.ClearingPrice.Should().Be(140m);
        result.VolumeSold.Should().Be(LotVolume);
        result.Revenue.Should().Be(LotVolume * 140m);
        result.Awards.Should().HaveCount(2);
        result.Awards[0].Bid.Unit.Should().BeSameAs(first);
        result.Awards[0].Volume.Should().Be(200_000m, "the higher bid is filled first");
        result.Awards[1].Volume.Should().Be(37_500m, "the marginal bid is filled to the offered volume");
        result.Awards[1].Cost.Should().Be(37_500m * 140m);
        auction.IsCleared.Should().BeTrue();
    }

    [Fact]
    public void When_demand_is_weaker_than_the_offer_the_auction_clears_at_the_floor()
    {
        Simulation simulation = TestSimulation.Build();
        Auction auction = FirstAuction(simulation);
        auction.PlaceBid(simulation.FindUnit(1), 1, 120m, 50_000m);

        AuctionResult result = auction.Clear().Single();

        result.ClearingPrice.Should().Be(100m);
        result.VolumeSold.Should().Be(50_000m);
        result.Revenue.Should().Be(5_000_000m);
        result.Awards.Should().ContainSingle().Which.Volume.Should().Be(50_000m);
    }

    [Fact]
    public void Bids_at_the_same_price_are_served_in_the_order_they_arrived()
    {
        Simulation simulation = TestSimulation.Build();
        Auction auction = FirstAuction(simulation);
        Unit early = simulation.FindUnit(1);
        Unit late = simulation.FindUnit(3);
        auction.PlaceBid(early, 1, 140m, 200_000m);
        auction.PlaceBid(late, 1, 140m, 200_000m);

        AuctionResult result = auction.Clear().Single();

        result.Awards.Should().HaveCount(2);
        result.Awards[0].Bid.Unit.Should().BeSameAs(early);
        result.Awards[0].Volume.Should().Be(200_000m);
        result.Awards[1].Bid.Unit.Should().BeSameAs(late);
        result.Awards[1].Volume.Should().Be(37_500m);
    }

    [Fact]
    public void An_auction_nobody_bids_in_sells_nothing_and_the_volume_stays_in_reserve()
    {
        Simulation simulation = TestSimulation.Build();
        Auction auction = FirstAuction(simulation);

        AuctionResult result = auction.Clear().Single();

        result.ClearingPrice.Should().BeNull();
        result.VolumeSold.Should().Be(0m);
        result.Revenue.Should().Be(0m);
        result.Awards.Should().BeEmpty();
        simulation.Government.Held(1).Should().Be(LotVolume, "unsold allowances stay with the government");
    }

    [Fact]
    public void Buyers_pay_the_clearing_price_and_receive_the_vintage_and_the_government_collects_the_money()
    {
        Simulation simulation = TestSimulation.Build();
        Auction auction = FirstAuction(simulation);
        Unit unit = simulation.FindUnit(1);
        Company company = unit.Company;
        decimal capital = company.Capital;
        auction.PlaceBid(unit, 1, 150m, 200_000m);

        AuctionResult result = auction.Clear().Single();

        company.Capital.Should().Be(capital - (200_000m * 100m));
        simulation.Ledger.Available(company, Product.Allowance(1)).Should().Be(200_000m);
        simulation.Government.Revenue.Should().Be(20_000_000m);
        result.VolumeSold.Should().Be(200_000m);
    }

    [Fact]
    public void Forward_and_current_vintages_clear_separately_with_their_own_prices()
    {
        Simulation simulation = TestSimulation.Build();
        AuctionSchedule schedule = AuctionSchedule.Build(simulation, forwardVintageShare: 0.20m);
        Auction auction = schedule.ForSection(1, 4);
        Unit unit = simulation.FindUnit(1);
        auction.PlaceBid(unit, 1, 150m, 100_000m);
        auction.PlaceBid(unit, 2, 130m, 100_000m);

        IReadOnlyList<AuctionResult> results = auction.Clear();

        results.Should().HaveCount(2);
        results[0].Vintage.Should().Be(1);
        results[0].ClearingPrice.Should().Be(100m, "vintage 1 is under-subscribed");
        results[1].Vintage.Should().Be(2);
        results[1].ClearingPrice.Should().Be(100m, "vintage 2 is under-subscribed too");
        results[1].VolumeSold.Should().Be(100_000m);
        simulation.Ledger.Available(unit.Company, Product.Allowance(2)).Should().Be(100_000m);
        simulation.Government.Held(2).Should().BeGreaterThan(0m, "the forward lot is only part of vintage 2");
    }

    [Fact]
    public void An_auction_can_only_be_cleared_once_and_closes_to_further_bids()
    {
        Simulation simulation = TestSimulation.Build();
        Auction auction = FirstAuction(simulation);
        auction.Clear();

        Action clearAgain = () => auction.Clear();
        Action bidAfterClose = () => auction.PlaceBid(simulation.FindUnit(1), 1, 150m, 1_000m);

        clearAgain.Should().Throw<InvalidOperationException>().WithMessage("*already*");
        bidAfterClose.Should().Throw<InvalidOperationException>().WithMessage("*closed*");
    }
}
