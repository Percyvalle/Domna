using Domna.Domain.Models;
using Domna.Domain.Parameters;

namespace Domna.Calculation;

/// <summary>
/// Encodes and decodes the charge-station parameters by pooling all portions of each
/// material type across drops and redistributing aggregate zone totals, so that the
/// optimizer can effectively transfer material between drops of the same type.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="MatrixStationConstraint"/>, which keeps each individual drop's free-zone
/// total fixed and only redistributes within that drop, this constraint treats all ore as one
/// pool and all coke as one pool. The optimizer moves the aggregate ore and coke counts between
/// zones; the per-drop allocation within each zone is then scaled proportionally from the
/// starting program.
/// </para>
/// <para>
/// For N free zones the search space has 2(N-1) dimensions: (N-1) coordinates for the ore
/// pool and (N-1) for the coke pool, using the same broken-stick parametrization as the
/// per-drop constraint.
/// </para>
/// </remarks>
internal sealed class AggregateStationConstraint : IMatrixConstraint
{
    // Per-drop zone counts in [dropIdx][zoneIdx], zoneIdx indexes _freeZoneBuckets.
    private readonly int[][] _oreDropZones;
    private readonly int[][] _cokeDropZones;

    // Aggregate zone totals summed across all drops of each material.
    private readonly int[] _oreAggByZone;
    private readonly int[] _cokeAggByZone;
    private readonly int _totalOreFree;
    private readonly int _totalCokeFree;
    private readonly int _totalOre;
    private readonly int _totalCoke;
    private readonly int[] _freeZoneBuckets;

    public AggregateStationConstraint(ChargeProgram program, int[]? freeZoneBuckets = null)
    {
        ArgumentNullException.ThrowIfNull(program);

        _freeZoneBuckets = freeZoneBuckets ?? [1, 2, 3];
        int n = _freeZoneBuckets.Length;

        List<int[]> oreDropZones = [];
        List<int[]> cokeDropZones = [];
        int totalOre = 0;
        int totalCoke = 0;

        foreach (ChargeDrop drop in program.Drops)
        {
            bool isOre = drop.Material == ChargeMaterial.Ore;
            int[] zoneCounts = new int[n];
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
        _totalOre = totalOre;
        _totalCoke = totalCoke;

        _oreAggByZone = new int[n];
        _cokeAggByZone = new int[n];

        foreach (int[] drop in _oreDropZones)
        {
            for (int zi = 0; zi < n; zi++)
            {
                _oreAggByZone[zi] += drop[zi];
            }
        }

        foreach (int[] drop in _cokeDropZones)
        {
            for (int zi = 0; zi < n; zi++)
            {
                _cokeAggByZone[zi] += drop[zi];
            }
        }

        _totalOreFree = _oreAggByZone.Sum();
        _totalCokeFree = _cokeAggByZone.Sum();
    }

    /// <summary>
    /// Number of extra search dimensions: (N-1) per material type that has drops,
    /// where N is the number of free zone buckets.
    /// </summary>
    public int Dimensions
    {
        get
        {
            if (_freeZoneBuckets.Length < 2)
            {
                return 0;
            }

            int perMaterial = _freeZoneBuckets.Length - 1;
            int materials = (_oreDropZones.Length > 0 ? 1 : 0) + (_cokeDropZones.Length > 0 ? 1 : 0);
            return perMaterial * materials;
        }
    }

    public bool IsMovable => _freeZoneBuckets.Length >= 2
        && (_totalOreFree > 0 || _totalCokeFree > 0);

    /// <summary>
    /// Encodes the aggregate zone totals as broken-stick coordinates in [0, 1]:
    /// ore pool first (N-1 coords), coke pool second (N-1 coords).
    /// </summary>
    public double[] Encode()
    {
        int n = _freeZoneBuckets.Length;
        if (n <= 1)
        {
            return [];
        }

        int perMaterial = n - 1;
        double[] coords = new double[Dimensions];
        int idx = 0;

        if (_oreDropZones.Length > 0)
        {
            EncodeAggregate(_oreAggByZone, coords, idx, perMaterial);
            idx += perMaterial;
        }

        if (_cokeDropZones.Length > 0)
        {
            EncodeAggregate(_cokeAggByZone, coords, idx, perMaterial);
        }

        return coords;
    }

    public ParameterSet Decode(ParameterSet parameters, ReadOnlySpan<double> coords)
    {
        (int[] oreZoneTotals, int[] cokeZoneTotals) = DecodeAggregateTotals(coords);
        for (int i = 0; i < _freeZoneBuckets.Length; i++)
        {
            parameters = ApplyShare(
                parameters,
                BucketToParameter(_freeZoneBuckets[i]),
                oreZoneTotals[i],
                cokeZoneTotals[i]);
        }

        return parameters;
    }

    /// <summary>
    /// Rebuilds <paramref name="original"/> so that the aggregate zone totals match the
    /// decoded values. Each drop's per-zone count is scaled proportionally from the starting
    /// program using largest-remainder rounding to preserve per-zone aggregate sums exactly.
    /// </summary>
    public ChargeProgram BuildUpdatedProgram(ChargeProgram original, ReadOnlySpan<double> coords)
    {
        ArgumentNullException.ThrowIfNull(original);

        (int[] newOreTotals, int[] newCokeTotals) = DecodeAggregateTotals(coords);

        int[][] oreNewZones = ScaleDropZones(_oreDropZones, _oreAggByZone, newOreTotals);
        int[][] cokeNewZones = ScaleDropZones(_cokeDropZones, _cokeAggByZone, newCokeTotals);

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

    private static void EncodeAggregate(int[] zoneCounts, double[] coords, int offset, int perMaterial)
    {
        int remaining = zoneCounts.Sum();
        for (int i = 0; i < perMaterial; i++)
        {
            coords[offset + i] = remaining > 0 ? (double)zoneCounts[i] / remaining : 0.0;
            remaining -= zoneCounts[i];
        }
    }

    private static int[] DecodeAggregate(ReadOnlySpan<double> coords, int total, int n)
    {
        if (n <= 0)
        {
            return [];
        }

        int[] counts = new int[n];
        int remaining = total;
        for (int i = 0; i < n - 1; i++)
        {
            counts[i] = (int)Math.Round(Math.Clamp(coords[i], 0.0, 1.0) * remaining);
            remaining -= counts[i];
        }

        counts[n - 1] = remaining;
        return counts;
    }

    // Scales each drop's per-zone count proportionally to match new aggregate totals.
    // Uses largest-remainder rounding so the per-zone sum is exactly newAggregates[zi].
    private static int[][] ScaleDropZones(int[][] dropZones, int[] originalAggregates, int[] newAggregates)
    {
        int dropCount = dropZones.Length;
        int n = originalAggregates.Length;
        int[][] result = new int[dropCount][];
        for (int d = 0; d < dropCount; d++)
        {
            result[d] = new int[n];
        }

        if (dropCount == 0)
        {
            return result;
        }

        for (int zi = 0; zi < n; zi++)
        {
            int origTotal = originalAggregates[zi];
            int newTotal = newAggregates[zi];

            if (origTotal == 0)
            {
                int each = newTotal / dropCount;
                int rem = newTotal % dropCount;
                for (int d = 0; d < dropCount; d++)
                {
                    result[d][zi] = each + (d < rem ? 1 : 0);
                }
            }
            else
            {
                // Largest-remainder rounding: compute ideal real values, floor them, then
                // give the leftover units to the drops with the largest fractional parts.
                int floorSum = 0;
                double[] remainders = new double[dropCount];
                int[] floors = new int[dropCount];

                for (int d = 0; d < dropCount; d++)
                {
                    double ideal = (double)dropZones[d][zi] / origTotal * newTotal;
                    floors[d] = (int)ideal;
                    remainders[d] = ideal - floors[d];
                    floorSum += floors[d];
                }

                int extra = newTotal - floorSum;
                int[] order = [.. Enumerable.Range(0, dropCount).OrderByDescending(d => remainders[d])];

                for (int d = 0; d < dropCount; d++)
                {
                    result[d][zi] = floors[d];
                }

                for (int i = 0; i < extra && i < dropCount; i++)
                {
                    result[order[i]][zi]++;
                }
            }
        }

        return result;
    }

    private static Parameter BucketToParameter(int bucket) => bucket switch
    {
        1 => Parameter.ChargeStations3To5,
        2 => Parameter.ChargeStations6To8,
        _ => Parameter.ChargeStations9To11,
    };

    private (int[] OreTotals, int[] CokeTotals) DecodeAggregateTotals(ReadOnlySpan<double> coords)
    {
        int n = _freeZoneBuckets.Length;
        int perMaterial = n <= 1 ? 0 : n - 1;
        int[] oreTotals = new int[n];
        int[] cokeTotals = new int[n];
        int idx = 0;

        if (_oreDropZones.Length > 0)
        {
            int[] decoded = DecodeAggregate(coords.Slice(idx, perMaterial), _totalOreFree, n);
            for (int zi = 0; zi < n; zi++)
            {
                oreTotals[zi] = decoded[zi];
            }

            idx += perMaterial;
        }

        if (_cokeDropZones.Length > 0)
        {
            int[] decoded = DecodeAggregate(coords.Slice(idx, perMaterial), _totalCokeFree, n);
            for (int zi = 0; zi < n; zi++)
            {
                cokeTotals[zi] = decoded[zi];
            }
        }

        return (oreTotals, cokeTotals);
    }

    // Rebuilds one drop: pinned zone stations are copied from the original; each free
    // zone's new total is distributed across its stations in the same proportion as the
    // original. When a drop had zero in a bucket originally, the new total is spread evenly.
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
            int firstStation = _freeZoneBuckets[zi] * 3;
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
