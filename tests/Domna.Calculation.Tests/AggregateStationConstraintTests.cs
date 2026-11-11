using Domna.Domain.Models;
using Domna.Domain.Optimization;
using Domna.Domain.Parameters;

namespace Domna.Calculation.Tests;

public class AggregateStationConstraintTests
{
    [Fact]
    public void Encode_ThenDecode_ReturnsOriginalShares()
    {
        ChargeProgram program = WorkbookRight();
        AggregateStationConstraint constraint = new(program);
        ChargeProgramCalculator calculator = new();

        double[] coords = constraint.Encode();
        ParameterSet decoded = constraint.Decode(ParameterSet.Defaults, coords);

        ChargeStationShares? expected = calculator.Calculate(program);
        Assert.NotNull(expected);
        Assert.Equal(expected.Value.Stations9To11, decoded[Parameter.ChargeStations9To11], 10);
        Assert.Equal(expected.Value.Stations6To8, decoded[Parameter.ChargeStations6To8], 10);
        Assert.Equal(expected.Value.Stations3To5, decoded[Parameter.ChargeStations3To5], 10);
    }

    [Fact]
    public void Dimensions_IsTwo_PerFreeZone_PerMaterial()
    {
        // 3 free zones, 2 materials (ore + coke) → 2 × (3-1) = 4 dimensions.
        AggregateStationConstraint constraint = new(WorkbookRight());
        Assert.Equal(4, constraint.Dimensions);
    }

    [Fact]
    public void Dimensions_OreOnly_IsNMinusOne()
    {
        int[] orePortions = new int[ChargeDrop.StationCount];
        orePortions[8] = 3; // station 9

        ChargeProgram oreOnly = new([new ChargeDrop(ChargeMaterial.Ore, orePortions)]);
        AggregateStationConstraint constraint = new(oreOnly);

        // 3 free zones, 1 material → (3-1) = 2 dimensions.
        Assert.Equal(2, constraint.Dimensions);
    }

    [Fact]
    public void Decode_AtZeroCoords_PutsAllMovableInZone9To11()
    {
        // All zeros: broken-stick sends the full pool to zone 9–11.
        AggregateStationConstraint constraint = new(WorkbookRight());

        ParameterSet result = constraint.Decode(ParameterSet.Defaults, new double[constraint.Dimensions]);

        // All ore in zone 9-11, all coke in zone 9-11.
        // ore fraction = totalOreFree/totalOre = 18/18 = 1,  coke fraction = 16/20 = 0.8
        // share9To11 = 1/(1+0.8) × 100 = 500/9 ≈ 55.6%
        Assert.Equal(500.0 / 9.0, result[Parameter.ChargeStations9To11], 10);
        Assert.Equal(0.0, result[Parameter.ChargeStations6To8], 10);
        Assert.Equal(0.0, result[Parameter.ChargeStations3To5], 10);
    }

    [Fact]
    public void Decode_RandomCoords_AlwaysProducesValidPercentages()
    {
        AggregateStationConstraint constraint = new(WorkbookRight());
        Random rng = new(42);

        for (int i = 0; i < 1000; i++)
        {
            double[] coords = new double[constraint.Dimensions];
            for (int j = 0; j < coords.Length; j++)
            {
                coords[j] = rng.NextDouble();
            }

            ParameterSet result = constraint.Decode(ParameterSet.Defaults, coords);

            Assert.InRange(result[Parameter.ChargeStations9To11], 0.0, 100.0);
            Assert.InRange(result[Parameter.ChargeStations6To8], 0.0, 100.0);
            Assert.InRange(result[Parameter.ChargeStations3To5], 0.0, 100.0);
        }
    }

    [Fact]
    public void IsMovable_EmptyProgram_ReturnsFalse()
    {
        AggregateStationConstraint constraint = new(new ChargeProgram([]));
        Assert.False(constraint.IsMovable);
    }

    [Fact]
    public void IsMovable_AllPortionsInFixedZone_ReturnsFalse()
    {
        int[] portions = new int[ChargeDrop.StationCount];
        portions[0] = 3;
        portions[1] = 3;

        ChargeProgram program = new([
            new ChargeDrop(ChargeMaterial.Ore, portions),
            new ChargeDrop(ChargeMaterial.Coke, portions),
        ]);

        AggregateStationConstraint constraint = new(program);
        Assert.False(constraint.IsMovable);
    }

    [Fact]
    public void BuildUpdatedProgram_PreservesAggregateTotals()
    {
        // After moving all material to zone 9-11 (zero coords), the aggregate ore and coke
        // zone totals should be what the decode produces.
        ChargeProgram program = WorkbookRight();
        AggregateStationConstraint constraint = new(program);
        ChargeProgramCalculator calculator = new();

        double[] zeros = new double[constraint.Dimensions];
        ChargeProgram updated = constraint.BuildUpdatedProgram(program, zeros);

        // Verify via the calculator that the resulting program's shares match the decoded parameters.
        ChargeStationShares? shares = calculator.Calculate(updated);
        Assert.NotNull(shares);

        ParameterSet decodedParams = constraint.Decode(ParameterSet.Defaults, zeros);
        Assert.Equal(decodedParams[Parameter.ChargeStations9To11], shares.Value.Stations9To11, 1);
        Assert.Equal(decodedParams[Parameter.ChargeStations6To8], shares.Value.Stations6To8, 1);
        Assert.Equal(decodedParams[Parameter.ChargeStations3To5], shares.Value.Stations3To5, 1);
    }

    [Fact]
    public void BuildUpdatedProgram_CanMoveAllMaterialToOneZone()
    {
        // A simple two-drop program, one ore and one coke. Moving everything to zone 9-11
        // gives a valid program that the calculator can evaluate.
        static int[] Portions(params (int Station, int Count)[] entries)
        {
            int[] p = new int[ChargeDrop.StationCount];
            foreach ((int s, int c) in entries)
            {
                p[s - 1] = c;
            }

            return p;
        }

        ChargeProgram program = new(
        [
            new ChargeDrop(ChargeMaterial.Ore,  Portions((9, 2), (6, 1), (3, 1))),
            new ChargeDrop(ChargeMaterial.Coke, Portions((11, 1), (7, 1), (4, 1))),
        ]);

        AggregateStationConstraint constraint = new(program);

        double[] zeros = new double[constraint.Dimensions];
        ChargeProgram updated = constraint.BuildUpdatedProgram(program, zeros);

        ChargeProgramCalculator calculator = new();
        Assert.NotNull(calculator.Calculate(updated));
    }

    [Fact]
    public void AggregateMode_OptimizerConvergesOnSimpleProgram()
    {
        // Single ore drop + single coke drop: aggregate mode is equivalent to per-drop,
        // so the optimizer can reach the target exactly (all material in zone 9-11).
        static int[] Portions(params (int Station, int Count)[] entries)
        {
            int[] p = new int[ChargeDrop.StationCount];
            foreach ((int s, int c) in entries)
            {
                p[s - 1] = c;
            }

            return p;
        }

        ChargeProgram program = new(
        [
            new ChargeDrop(ChargeMaterial.Ore,  Portions((9, 2), (8, 1), (6, 1), (5, 1), (3, 1))),
            new ChargeDrop(ChargeMaterial.Coke, Portions((11, 1), (10, 1), (7, 1), (4, 1))),
        ]);

        TemperatureProfileCalculator calculator = new();
        AggregateStationConstraint constraint = new(program);

        ParameterSet startPs = constraint.Decode(ParameterSet.Defaults, constraint.Encode());
        ParameterSet targetPs = constraint.Decode(ParameterSet.Defaults, new double[constraint.Dimensions]);
        TemperatureProfile target = calculator.Calculate(targetPs);

        OptimizationRequest request = new()
        {
            Start = startPs,
            Target = target,
            FreeParameters = [],
            StationMode = new ChargeStationMode.Matrix(program, MatrixOptimizerMode.Aggregate),
        };

        DifferentialEvolutionOptimizer optimizer = new(calculator);
        OptimizationOutcome outcome = optimizer.Optimize(request, null, CancellationToken.None);

        Assert.False(outcome.Cancelled);
        Assert.True(outcome.Residual < 1e-10, $"residual too large: {outcome.Residual:e3}");
    }

    private static ChargeProgram WorkbookRight()
    {
        static int[] Portions(params (int Station, int Count)[] entries)
        {
            int[] p = new int[ChargeDrop.StationCount];
            foreach ((int s, int c) in entries)
            {
                p[s - 1] = c;
            }

            return p;
        }

        List<ChargeDrop> drops =
        [
            new(ChargeMaterial.Coke, Portions((8, 1), (7, 1), (6, 1))),
            new(ChargeMaterial.Coke, Portions((5, 1), (4, 1), (3, 1))),
            new(ChargeMaterial.Ore,  Portions((9, 2), (8, 1))),
            new(ChargeMaterial.Ore,  Portions((6, 1), (5, 1), (4, 1))),
            new(ChargeMaterial.Coke, Portions((8, 1), (7, 1), (6, 1))),
            new(ChargeMaterial.Coke, Portions((3, 1), (2, 2))),
            new(ChargeMaterial.Coke, Portions((10, 1), (9, 1), (8, 1))),
            new(ChargeMaterial.Ore,  Portions((7, 1), (6, 1), (5, 1))),
            new(ChargeMaterial.Ore,  Portions((8, 1), (7, 1), (6, 1))),
            new(ChargeMaterial.Coke, Portions((6, 1), (5, 1), (4, 1))),
            new(ChargeMaterial.Coke, Portions((2, 2))),
            new(ChargeMaterial.Ore,  Portions((10, 1), (9, 1), (8, 1))),
            new(ChargeMaterial.Ore,  Portions((7, 1), (6, 1), (5, 1))),
        ];

        return new ChargeProgram(drops);
    }
}
