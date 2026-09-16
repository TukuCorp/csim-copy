using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Randomness;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Randomness;

public sealed class SimulationRandomTests
{
    [Fact]
    public void The_same_seed_produces_the_same_stream_and_a_different_seed_does_not()
    {
        SimulationRandom first = new(20240916UL);
        SimulationRandom second = new(20240916UL);
        SimulationRandom other = new(20240917UL);

        ulong[] draws = [.. Enumerable.Range(0, 16).Select(_ => first.NextUInt64())];
        ulong[] repeat = [.. Enumerable.Range(0, 16).Select(_ => second.NextUInt64())];
        ulong[] different = [.. Enumerable.Range(0, 16).Select(_ => other.NextUInt64())];

        draws.Should().Equal(repeat);
        draws.Should().NotEqual(different);
    }

    [Fact]
    public void Whole_numbers_stay_inside_the_range_and_spread_out()
    {
        SimulationRandom random = new(7UL);
        int[] counts = new int[6];

        for (int draw = 0; draw < 6_000; draw++)
        {
            int value = random.NextInt(6);
            value.Should().BeInRange(0, 5);
            counts[value]++;
        }

        counts.Should().OnlyContain(count => count > 800, "6000 draws over 6 values should be spread evenly");
    }

    [Fact]
    public void A_range_with_no_room_is_rejected()
    {
        SimulationRandom random = new(7UL);

        Action zero = () => random.NextInt(0);

        zero.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Decimal_draws_stay_inside_the_range_even_when_rounding_would_step_out_of_it()
    {
        SimulationRandom random = new(11UL);

        for (int draw = 0; draw < 1_000; draw++)
        {
            decimal narrow = random.NextDecimal(0.0000004m, 0.0000006m);
            narrow.Should().BeInRange(0.0000004m, 0.0000006m);

            decimal growth = random.NextDecimal(0.02m, 0.06m);
            growth.Should().BeInRange(0.02m, 0.06m);
        }
    }

    [Fact]
    public void A_wide_range_covers_both_ends_of_the_band()
    {
        SimulationRandom random = new(13UL);
        decimal[] draws = [.. Enumerable.Range(0, 200).Select(_ => random.NextDecimal(0.02m, 0.06m))];

        draws.Min().Should().BeGreaterThanOrEqualTo(0.02m);
        draws.Max().Should().BeLessThanOrEqualTo(0.06m);
        draws.Min().Should().BeLessThan(0.03m, "draws should reach the low half of the band");
        draws.Max().Should().BeGreaterThan(0.05m, "draws should reach the high half of the band");
    }

    [Fact]
    public void An_identifier_is_derived_from_the_seed_and_nothing_else()
    {
        SimulationRandom.IdFromSeed(20240916UL).Should().Be(SimulationRandom.IdFromSeed(20240916UL));
        SimulationRandom.IdFromSeed(20240916UL).Should().NotBe(SimulationRandom.IdFromSeed(20240917UL));
        SimulationRandom.IdFromSeed(0UL).Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void A_simulations_stream_is_its_own_and_is_reproducible_from_its_seed()
    {
        Simulation first = TestSimulation.Build(seed: 99UL);
        Simulation second = TestSimulation.Build(seed: 99UL);
        Simulation other = TestSimulation.Build(seed: 100UL);

        first.Seed.Should().Be(99UL);
        first.Random.Seed.Should().Be(99UL);
        first.Random.NextUInt64().Should().Be(second.Random.NextUInt64());
        TestSimulation.Build(seed: 99UL).Random.NextUInt64().Should().NotBe(other.Random.NextUInt64());
    }
}
