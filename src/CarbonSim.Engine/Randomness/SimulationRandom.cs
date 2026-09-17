using System.Buffers.Binary;

namespace CarbonSim.Engine.Randomness;

/// <summary>
/// The single random stream of a simulation. SplitMix64 is pinned deliberately: runs have to
/// be reproducible for training and for the fidelity tests, and the algorithm of
/// <see cref="System.Random"/> for a given seed is not guaranteed across .NET versions.
/// </summary>
public sealed class SimulationRandom
{
    private const ulong Increment = 0x9E3779B97F4A7C15UL;
    private const ulong MixA = 0xBF58476D1CE4E5B9UL;
    private const ulong MixB = 0x94D049BB133111EBUL;

    /// <summary>Keeps the identifier stream independent of the first draws of the simulation stream.</summary>
    private const ulong IdSalt = 0xD1B54A32D192ED03UL;

    private ulong _state;

    public SimulationRandom(ulong seed)
    {
        Seed = seed;
        _state = seed;
    }

    public ulong Seed { get; }

    /// <summary>
    /// Where the stream has got to. Capturing this is what lets a run be saved mid-flight: the
    /// position is restored as it was rather than replayed, so the next draw is the one the
    /// original would have made.
    /// </summary>
    internal ulong State => _state;

    /// <summary>Puts the stream back where a snapshot found it.</summary>
    internal void RestoreState(ulong state) => _state = state;

    public ulong NextUInt64()
    {
        _state += Increment;
        ulong z = _state;
        z = (z ^ (z >> 30)) * MixA;
        z = (z ^ (z >> 27)) * MixB;
        return z ^ (z >> 31);
    }

    /// <summary>A double in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>An integer in [0, <paramref name="maxExclusive"/>), without modulo bias.</summary>
    public int NextInt(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxExclusive, 1);

        ulong range = (ulong)maxExclusive;
        ulong limit = range * (ulong.MaxValue / range);
        ulong value;

        do
        {
            value = NextUInt64();
        }
        while (value >= limit);

        return (int)(value % range);
    }

    /// <summary>A value in [<paramref name="minInclusive"/>, <paramref name="maxInclusive"/>], rounded to a number of decimals.</summary>
    public decimal NextDecimal(decimal minInclusive, decimal maxInclusive, int decimals = 6)
    {
        if (maxInclusive < minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInclusive), maxInclusive, "The upper bound cannot be below the lower bound.");
        }

        decimal value = minInclusive + ((decimal)NextDouble() * (maxInclusive - minInclusive));
        decimal rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);

        // Rounding can step past either bound when the bounds are finer than the rounding
        // step, so the result is clamped back into the range the caller asked for.
        return rounded < minInclusive ? minInclusive : rounded > maxInclusive ? maxInclusive : rounded;
    }

    /// <summary>
    /// Derives the simulation identifier from its seed, so a run can be reproduced from the
    /// seed alone.
    /// </summary>
    public static Guid IdFromSeed(ulong seed)
    {
        SimulationRandom stream = new(seed ^ IdSalt);
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[..8], stream.NextUInt64());
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..], stream.NextUInt64());

        return new Guid(bytes);
    }
}
