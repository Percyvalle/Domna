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
/// Movement is strictly horizontal: for each individual drop (row in the charge
/// matrix) the optimizer may redistribute its portions between zone buckets, but
/// the per-drop total across all free zones is fixed. No material crosses from one
/// drop to another.
/// </para>
/// <para>
/// The search space for the station part is <see cref="Dimensions"/>-dimensional: a
/// broken-stick parametrization per drop, over the free zone buckets separately for
/// each ore and each coke drop. For N free zones and D drops of one material,
/// (N-1)×D coordinates are needed. Each coordinate is a fraction in [0, 1], so the
/// full coordinate vector stays within the unit box that the differential-evolution
/// and Nelder–Mead algorithms expect.
/// </para>
/// <para>
/// Every evaluation decodes fractional coordinates into integer counts per drop and
/// per zone, then aggregates to zone totals for the ore-share formula used by
/// <see cref="ChargeProgramCalculator"/>. Rounding creates a discrete grid of
/// achievable share values; the density of this grid grows with the total portion
/// count, so a richer program gives the optimizer finer resolution.
/// </para>
/// </remarks>
internal sealed class MatrixStationConstraint
{
    // Bucket layout: index 0 = stations 1–2 (pinned), 1 = 3–5, 2 = 6–8, 3 = 9–11.
    // Per-drop zone counts: [dropIndex][zoneIndex] where zoneIndex indexes into _freeZoneBuckets.
    private readonly int[][] _oreDropZones;
    private readonly int[][] _cokeDropZones;
    private readonly int[] _oreDropFreeTotals;
    private readonly int[] _cokeDropFreeTotals;
    private readonly int _totalOre;
    private readonly int _totalCoke;
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

        _freeZoneBuckets = freeZoneBuckets ?? [1, 2, 3];

        List<int[]> oreDropZones = [];
        List<int[]> cokeDropZones = [];
        int totalOre = 0;
        int totalCoke = 0;

        foreach (ChargeDrop drop in program.Drops)
        {
            bool isOre = drop.Material == ChargeMaterial.Ore;
            int[] zoneCounts = new int[_freeZoneBuckets.Length];
            int dropTotal = 0;

            for (int s = 1; s <= ChargeDrop.StationCount; s++)
            {
                int count = drop[s];
                dropTotal += count;
                int zi = Array.IndexOf(_freeZoneBuckets, s / 3);
                if (zi >= 0)
                {
                    zoneCounts[zi] += count;
                }
            }

            if (isOre)
            {
                oreDropZones.Add(zoneCounts);
                totalOre += dropTotal;
            }
            else
            {
                cokeDropZones.Add(zoneCounts);
                totalCoke += dropTotal;
            }
        }

        _oreDropZones = [.. oreDropZones];
        _cokeDropZones = [.. cokeDropZones];
        _oreDropFreeTotals = [.. _oreDropZones.Select(d => d.Sum())];
        _cokeDropFreeTotals = [.. _cokeDropZones.Select(d => d.Sum())];
        _totalOre = totalOre;
        _totalCoke = totalCoke;
    }

    /// <summary>
    /// Number of extra search dimensions this constraint contributes beyond the
    /// non-station free parameters: (N-1) per drop for each of ore and coke drops,
    /// where N is the number of free zone buckets. Zero when fewer than two zones are free.
    /// </summary>
    public int Dimensions
    {
        get
        {
            if (_freeZoneBuckets.Length < 2)
            {
                return 0;
            }

            int perDrop = _freeZoneBuckets.Length - 1;
            return perDrop * (_oreDropZones.Length + _cokeDropZones.Length);
        }
    }

    /// <summary>
    /// Whether there are any portions to redistribute between zones.
    /// False means the matrix has no movable material or only one free zone,
    /// so the optimizer has no effect on the station parameters.
    /// </summary>
    public bool IsMovable => _freeZoneBuckets.Length >= 2
        && (_oreDropFreeTotals.Sum() > 0 || _cokeDropFreeTotals.Sum() > 0);

    /// <summary>
    /// Encodes the initial portion distribution from the program as coordinates
    /// in [0, 1], ready to seed the differential-evolution population.
    /// Each drop is encoded independently using a broken-stick parametrization
    /// over its own free-zone total.
    /// </summary>
    public double[] Encode()
    {
        int n = _freeZoneBuckets.Length;
        if (n <= 1)
        {
            return [];
        }

        int perDrop = n - 1;
        double[] coords = new double[Dimensions];
        int idx = 0;

        foreach (int[] drop in _oreDropZones)
        {
            EncodeDrop(drop, coords, idx, perDrop);
            idx += perDrop;
        }

        foreach (int[] drop in _cokeDropZones)
        {
            EncodeDrop(drop, coords, idx, perDrop);
            idx += perDrop;
        }

        return coords;
    }

    /// <summary>
    /// Decodes coordinates in [0, 1] into integer ore and coke counts for each drop
    /// and each free zone, aggregates to zone totals, computes the ore share for each
    /// zone, and applies the resulting share values to <paramref name="parameters"/>.
    /// </summary>
    public ParameterSet Decode(ParameterSet parameters, ReadOnlySpan<double> coords)
    {
        (int[] oreZoneTotals, int[] cokeZoneTotals) = DecodeZoneTotals(coords);
        for (int i = 0; i < _freeZoneBuckets.Length; i++)
        {
            parameters = ApplyShare(parameters, BucketToParameter(_freeZoneBuckets[i]), oreZoneTotals[i], cokeZoneTotals[i]);
        }

        return parameters;
    }

    /// <summary>
    /// Rebuilds <paramref name="original"/> so that the integer portions in the free
    /// zone stations reflect what <paramref name="coords"/> decoded to. Each drop is
    /// rebuilt independently: its free-zone total is preserved, only the distribution
    /// between zones changes. The number of drops, their order, their material type,
    /// and all fixed-zone station values are preserved.
    /// </summary>
    public ChargeProgram BuildUpdatedProgram(ChargeProgram original, ReadOnlySpan<double> coords)
    {
        ArgumentNullException.ThrowIfNull(original);

        int n = _freeZoneBuckets.Length;
        int perDrop = n <= 1 ? 0 : n - 1;

        // Decode all ore drops first, then all coke drops — matching the encoding order.
        int[][] oreNewZones = new int[_oreDropZones.Length][];
        int coordIdx = 0;
        for (int j = 0; j < _oreDropZones.Length; j++)
        {
            oreNewZones[j] = DecodeDrop(coords.Slice(coordIdx, perDrop), _oreDropFreeTotals[j], n);
            coordIdx += perDrop;
        }

        int[][] cokeNewZones = new int[_cokeDropZones.Length][];
        for (int j = 0; j < _cokeDropZones.Length; j++)
        {
            cokeNewZones[j] = DecodeDrop(coords.Slice(coordIdx, perDrop), _cokeDropFreeTotals[j], n);
            coordIdx += perDrop;
        }

        // Rebuild drops in original program order.
        int dropCount = original.Drops.Count;
        ChargeDrop[] result = new ChargeDrop[dropCount];
        int oreIdx = 0;
        int cokeIdx = 0;

        for (int d = 0; d < dropCount; d++)
        {
            ChargeDrop drop = original.Drops[d];
            result[d] = drop.Material == ChargeMaterial.Ore
                ? RebuildDrop(drop, oreNewZones[oreIdx++])
                : RebuildDrop(drop, cokeNewZones[cokeIdx++]);
        }

        return new ChargeProgram(result);
    }

    private static void EncodeDrop(int[] zones, double[] coords, int offset, int perDrop)
    {
        int remaining = zones.Sum();
        for (int i = 0; i < perDrop; i++)
        {
            coords[offset + i] = remaining > 0 ? (double)zones[i] / remaining : 0.0;
            remaining -= zones[i];
        }
    }

    private static int[] DecodeDrop(ReadOnlySpan<double> coords, int freeTotal, int n)
    {
        if (n <= 0)
        {
            return [];
        }

        int[] counts = new int[n];
        int remaining = freeTotal;
        for (int i = 0; i < n - 1; i++)
        {
            counts[i] = (int)Math.Round(Math.Clamp(coords[i], 0.0, 1.0) * remaining);
            remaining -= counts[i];
        }

        counts[n - 1] = remaining;
        return counts;
    }

    private static Parameter BucketToParameter(int bucket) => bucket switch
    {
        1 => Parameter.ChargeStations3To5,
        2 => Parameter.ChargeStations6To8,
        _ => Parameter.ChargeStations9To11,
    };

    private (int[] OreTotals, int[] CokeTotals) DecodeZoneTotals(ReadOnlySpan<double> coords)
    {
        int n = _freeZoneBuckets.Length;
        int perDrop = n <= 1 ? 0 : n - 1;
        int[] oreTotals = new int[n];
        int[] cokeTotals = new int[n];
        int idx = 0;

        for (int j = 0; j < _oreDropZones.Length; j++)
        {
            int[] counts = DecodeDrop(coords.Slice(idx, perDrop), _oreDropFreeTotals[j], n);
            idx += perDrop;
            for (int i = 0; i < n; i++)
            {
                oreTotals[i] += counts[i];
            }
        }

        for (int j = 0; j < _cokeDropZones.Length; j++)
        {
            int[] counts = DecodeDrop(coords.Slice(idx, perDrop), _cokeDropFreeTotals[j], n);
            idx += perDrop;
            for (int i = 0; i < n; i++)
            {
                cokeTotals[i] += counts[i];
            }
        }

        return (oreTotals, cokeTotals);
    }

    // Rebuilds one drop: pinned zone stations are copied from the original; each free
    // zone's new total is distributed across its three stations in the same proportion
    // as the original. When a drop had zero in a bucket originally, the new total is
    // spread evenly across the three stations.
    private ChargeDrop RebuildDrop(ChargeDrop original, int[] newZoneCounts)
    {
        int[] portions = new int[ChargeDrop.StationCount];

        for (int s = 1; s <= ChargeDrop.StationCount; s++)
        {
            int bucket = s / 3;
            if (Array.IndexOf(_freeZoneBuckets, bucket) < 0)
            {
                portions[s - 1] = original[s];
            }
        }

        for (int zi = 0; zi < _freeZoneBuckets.Length; zi++)
        {
            int firstStation = _freeZoneBuckets[zi] * 3; // 1-based: 3, 6, or 9
            int newBucketTotal = newZoneCounts[zi];

            int origS0 = original[firstStation];
            int origS1 = original[firstStation + 1];
            int origS2 = original[firstStation + 2];
            int origBucketTotal = origS0 + origS1 + origS2;

            int s0, s1, s2;
            if (origBucketTotal == 0)
            {
                int each = newBucketTotal / 3;
                int rem = newBucketTotal % 3;
                s0 = each + (rem > 0 ? 1 : 0);
                s1 = each + (rem > 1 ? 1 : 0);
                s2 = each;
            }
            else
            {
                s0 = (int)Math.Round((double)origS0 / origBucketTotal * newBucketTotal);
                s0 = Math.Min(s0, newBucketTotal);
                s1 = (int)Math.Round((double)origS1 / origBucketTotal * newBucketTotal);
                s1 = Math.Min(s1, newBucketTotal - s0);
                s2 = newBucketTotal - s0 - s1;
            }

            portions[firstStation - 1] = s0;
            portions[firstStation] = s1;
            portions[firstStation + 1] = s2;
        }

        return new ChargeDrop(original.Material, portions);
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
