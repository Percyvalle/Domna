using Domna.Domain.Models;
using Domna.Domain.Parameters;

namespace Domna.Calculation;

/// <summary>
/// Encodes and decodes the charge-station parameters as integer portion counts
/// derived from a <see cref="ChargeProgram"/>, so that every candidate the optimizer
/// evaluates corresponds to a physically realizable zone distribution.
/// </summary>
/// <remarks>
/// <para>
/// The furnace has eleven stations grouped into four buckets by integer division
/// (station / 3): bucket 0 covers stations 1–2 and is pinned; buckets 1–3 cover
/// stations 3–5, 6–8, and 9–11 respectively and may be freely moved by the optimizer.
/// </para>
/// <para>
/// The search space for the station part is <see cref="Dimensions"/>-dimensional: a
/// broken-stick parametrization over the free zone buckets, separately for ore and coke.
/// For N free zones, (N-1) coordinates per material are needed, for 2*(N-1) total.
/// Each coordinate is a fraction in [0, 1], so the full coordinate vector stays within
/// the unit box that the differential-evolution and Nelder–Mead algorithms expect.
/// </para>
/// <para>
/// Every evaluation decodes fractional coordinates into integer counts, then computes
/// the ore share for each zone using the same formula as <see cref="ChargeProgramCalculator"/>.
/// Rounding creates a discrete grid of achievable share values; the density of this grid
/// grows with the total portion count, so a richer program gives the optimizer finer resolution.
/// </para>
/// </remarks>
internal sealed class MatrixStationConstraint
{
    // Bucket layout: index 0 = stations 1–2 (pinned), 1 = 3–5, 2 = 6–8, 3 = 9–11.
    private readonly int[] _ore;
    private readonly int[] _coke;
    private readonly int _totalOre;
    private readonly int _totalCoke;
    private readonly int _movableOre;
    private readonly int _movableCoke;
    private readonly int[] _freeZoneBuckets;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatrixStationConstraint"/> class
    /// from a charge program and an optional zone selection.
    /// </summary>
    /// <param name="program">The charge program to encode.</param>
    /// <param name="freeZoneBuckets">
    /// Sorted bucket indices (1, 2, 3) that the optimizer may redistribute between.
    /// Pass <see langword="null"/> to leave all three zones free (the default).
    /// </param>
    public MatrixStationConstraint(ChargeProgram program, int[]? freeZoneBuckets = null)
    {
        ArgumentNullException.ThrowIfNull(program);

        _ore = new int[4];
        _coke = new int[4];

        foreach (ChargeDrop drop in program.Drops)
        {
            int[] target = drop.Material == ChargeMaterial.Coke ? _coke : _ore;
            for (int s = 1; s <= ChargeDrop.StationCount; s++)
            {
                target[s / 3] += drop[s];
            }
        }

        _totalOre = _ore[0] + _ore[1] + _ore[2] + _ore[3];
        _totalCoke = _coke[0] + _coke[1] + _coke[2] + _coke[3];

        _freeZoneBuckets = freeZoneBuckets ?? [1, 2, 3];
        _movableOre = _freeZoneBuckets.Sum(b => _ore[b]);
        _movableCoke = _freeZoneBuckets.Sum(b => _coke[b]);
    }

    /// <summary>
    /// Number of extra search dimensions this constraint contributes beyond the
    /// non-station free parameters: (N-1) for ore and (N-1) for coke, where N is
    /// the number of free zone buckets. Zero when fewer than two zones are free.
    /// </summary>
    public int Dimensions => Math.Max(0, _freeZoneBuckets.Length - 1) * 2;

    /// <summary>
    /// Whether there are any portions to redistribute between zones.
    /// False means the matrix has no movable material or only one free zone,
    /// so the optimizer has no effect on the station parameters.
    /// </summary>
    public bool IsMovable => _freeZoneBuckets.Length >= 2 && (_movableOre > 0 || _movableCoke > 0);

    /// <summary>
    /// Encodes the initial portion distribution from the program as coordinates
    /// in [0, 1], ready to seed the differential-evolution population.
    /// </summary>
    public double[] Encode()
    {
        int n = _freeZoneBuckets.Length;
        if (n <= 1)
        {
            return [];
        }

        double[] coords = new double[Dimensions];
        int remainingOre = _movableOre;
        int remainingCoke = _movableCoke;

        for (int i = 0; i < n - 1; i++)
        {
            coords[i] = Fraction(_ore[_freeZoneBuckets[i]], remainingOre);
            remainingOre -= _ore[_freeZoneBuckets[i]];
            coords[n - 1 + i] = Fraction(_coke[_freeZoneBuckets[i]], remainingCoke);
            remainingCoke -= _coke[_freeZoneBuckets[i]];
        }

        return coords;
    }

    /// <summary>
    /// Decodes coordinates in [0, 1] into integer ore and coke counts for the free
    /// zones, computes the ore share for each zone, and applies the resulting share
    /// values to <paramref name="parameters"/>.
    /// </summary>
    public ParameterSet Decode(ParameterSet parameters, ReadOnlySpan<double> coords)
    {
        (int[] oreCounts, int[] cokeCounts) = DecodeIntegerCounts(coords);
        for (int i = 0; i < _freeZoneBuckets.Length; i++)
        {
            parameters = ApplyShare(parameters, BucketToParameter(_freeZoneBuckets[i]), oreCounts[i], cokeCounts[i]);
        }

        return parameters;
    }

    /// <summary>
    /// Rebuilds <paramref name="original"/> so that the integer portions in the free
    /// zone stations reflect what <paramref name="coords"/> decoded to. The number of
    /// drops, their order, their material type, and all fixed-zone station values are
    /// preserved; only the free-zone station values are updated.
    /// </summary>
    public ChargeProgram BuildUpdatedProgram(ChargeProgram original, ReadOnlySpan<double> coords)
    {
        ArgumentNullException.ThrowIfNull(original);

        (int[] oreCounts, int[] cokeCounts) = DecodeIntegerCounts(coords);

        ChargeDrop[] drops = [.. original.Drops];
        int dropCount = drops.Length;

        // Seed the mutable portions from the original drops; fixed zones remain untouched.
        int[][] portions = new int[dropCount][];
        for (int i = 0; i < dropCount; i++)
        {
            portions[i] = ReadPortions(drops[i]);
        }

        for (int zi = 0; zi < _freeZoneBuckets.Length; zi++)
        {
            int firstStation = _freeZoneBuckets[zi] * 3; // 1-based: 3, 6, or 9
            RedistributeMaterial(drops, portions, firstStation, ChargeMaterial.Ore, oreCounts[zi]);
            RedistributeMaterial(drops, portions, firstStation, ChargeMaterial.Coke, cokeCounts[zi]);
        }

        ChargeDrop[] result = new ChargeDrop[dropCount];
        for (int i = 0; i < dropCount; i++)
        {
            result[i] = new ChargeDrop(drops[i].Material, portions[i]);
        }

        return new ChargeProgram(result);
    }

    private static double Fraction(int count, int total) =>
        total > 0 ? (double)count / total : 0.0;

    private static int IntCount(double fraction, int total) =>
        (int)Math.Round(Math.Clamp(fraction, 0.0, 1.0) * total);

    private static int[] ReadPortions(ChargeDrop drop)
    {
        int[] p = new int[ChargeDrop.StationCount];
        for (int s = 1; s <= ChargeDrop.StationCount; s++)
        {
            p[s - 1] = drop[s];
        }

        return p;
    }

    private static void RedistributeMaterial(
        ChargeDrop[] drops, int[][] portions, int firstStation, ChargeMaterial material, int targetTotal)
    {
        int dropCount = drops.Length;

        // Collect matching drop indices and their original station totals in the bucket.
        int[] matchIndices = new int[dropCount];
        int matchCount = 0;
        for (int i = 0; i < dropCount; i++)
        {
            if (drops[i].Material == material)
            {
                matchIndices[matchCount++] = i;
            }
        }

        if (matchCount == 0)
        {
            return;
        }

        int[] origTotals = new int[matchCount];
        int grandTotal = 0;
        for (int m = 0; m < matchCount; m++)
        {
            int idx = matchIndices[m];
            int t = drops[idx][firstStation] + drops[idx][firstStation + 1] + drops[idx][firstStation + 2];
            origTotals[m] = t;
            grandTotal += t;
        }

        // Distribute targetTotal across matching drops proportionally to original totals.
        // Drops with zero original count in this bucket keep zero; when grandTotal is zero
        // (material was absent) the whole new count goes to the first drop.
        int[] newTotals = new int[matchCount];
        if (grandTotal == 0)
        {
            newTotals[0] = targetTotal;
        }
        else
        {
            int remaining = targetTotal;
            for (int m = 0; m < matchCount - 1; m++)
            {
                int share = (int)Math.Round((double)origTotals[m] / grandTotal * targetTotal);
                newTotals[m] = share;
                remaining -= share;
            }

            newTotals[matchCount - 1] = remaining;
        }

        // Distribute each drop's new bucket total across the three zone stations
        // in the same proportion as the original, with the remainder going to the last.
        for (int m = 0; m < matchCount; m++)
        {
            int idx = matchIndices[m];
            int origS0 = drops[idx][firstStation];
            int origS1 = drops[idx][firstStation + 1];
            int origS2 = drops[idx][firstStation + 2];
            int origDropTotal = origS0 + origS1 + origS2;
            int newDropTotal = newTotals[m];

            int s0, s1, s2;
            if (origDropTotal == 0)
            {
                int each = newDropTotal / 3;
                int rem = newDropTotal % 3;
                s0 = each + (rem > 0 ? 1 : 0);
                s1 = each + (rem > 1 ? 1 : 0);
                s2 = each;
            }
            else
            {
                s0 = (int)Math.Round((double)origS0 / origDropTotal * newDropTotal);
                s0 = Math.Min(s0, newDropTotal);
                s1 = (int)Math.Round((double)origS1 / origDropTotal * newDropTotal);
                s1 = Math.Min(s1, newDropTotal - s0);
                s2 = newDropTotal - s0 - s1;
            }

            // Portions array is 0-based; firstStation is 1-based.
            portions[idx][firstStation - 1] = s0;
            portions[idx][firstStation] = s1;
            portions[idx][firstStation + 1] = s2;
        }
    }

    private static Parameter BucketToParameter(int bucket) => bucket switch
    {
        1 => Parameter.ChargeStations3To5,
        2 => Parameter.ChargeStations6To8,
        _ => Parameter.ChargeStations9To11,
    };

    private (int[] OreCounts, int[] CokeCounts) DecodeIntegerCounts(ReadOnlySpan<double> coords)
    {
        int n = _freeZoneBuckets.Length;
        int[] oreCounts = new int[n];
        int[] cokeCounts = new int[n];
        int remainingOre = _movableOre;
        int remainingCoke = _movableCoke;

        for (int i = 0; i < n - 1; i++)
        {
            oreCounts[i] = IntCount(coords[i], remainingOre);
            remainingOre -= oreCounts[i];
            cokeCounts[i] = IntCount(coords[n - 1 + i], remainingCoke);
            remainingCoke -= cokeCounts[i];
        }

        if (n > 0)
        {
            oreCounts[n - 1] = remainingOre;
            cokeCounts[n - 1] = remainingCoke;
        }

        return (oreCounts, cokeCounts);
    }

    private ParameterSet ApplyShare(ParameterSet parameters, Parameter param, int ore, int coke)
    {
        if (_totalOre <= 0 || _totalCoke <= 0)
        {
            return parameters;
        }

        double oreFraction = (double)ore / _totalOre;
        double cokeFraction = (double)coke / _totalCoke;
        double combined = oreFraction + cokeFraction;
        return parameters.With(param, combined > 0.0 ? oreFraction / combined * 100.0 : 0.0);
    }
}
