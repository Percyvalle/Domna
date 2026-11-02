namespace Domna.Calculation;

/// <summary>
/// The pseudo-random sequence the optimizer searches with.
/// </summary>
/// <remarks>
/// It is written out here rather than taken from <see cref="System.Random"/> on
/// purpose. The calculation layer must be reproducible, and a seeded
/// <see cref="System.Random"/> only promises to repeat itself within one runtime
/// version — this SplitMix64 stream is the same on every machine and every
/// framework release, so a search can be reproduced from its seed alone. Nothing
/// here reads a clock or an entropy source.
/// </remarks>
internal sealed class SearchSequence
{
    private ulong _state;

    public SearchSequence(ulong seed) => _state = seed;

    /// <summary>The next value in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>The next value in [0, <paramref name="exclusiveMaximum"/>).</summary>
    public int NextIndex(int exclusiveMaximum) => (int)(NextUInt64() % (ulong)exclusiveMaximum);

    private ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
