using Domna.Domain.Models;
using Domna.Domain.Optimization;
using Domna.Domain.Parameters;

namespace Domna.Calculation.Tests;

public class MatrixStationConstraintTests
{
    [Fact]
    public void Encode_ThenDecode_ReturnsOriginalShares()
    {
        ChargeProgram program = WorkbookRight();
        MatrixStationConstraint constraint = new(program);
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
    public void Decode_MatchesWorkbookValues()
    {
        // The right block of the Excel workbook gives: 68.966%, 58.140%, 42.553%.
        ChargeProgram program = WorkbookRight();
        MatrixStationConstraint constraint = new(program);
        double[] coords = constraint.Encode();
        ParameterSet result = constraint.Decode(ParameterSet.Defaults, coords);

        Assert.Equal(68.96551724137932, result[Parameter.ChargeStations9To11], 10);
        Assert.Equal(58.139534883720934, result[Parameter.ChargeStations6To8], 10);
        Assert.Equal(42.5531914893617, result[Parameter.ChargeStations3To5], 8);
    }

    [Fact]
    public void Decode_AllMaterialsInOneZone_GivesExpectedShares()
    {
        // All ore in zone 9-11, all coke in zone 3-5 → ore share 100% at centre, 0% at periphery.
        int[] allOreInZone3 = new int[ChargeDrop.StationCount];
        allOreInZone3[8] = 1;
        allOreInZone3[9] = 1;
        allOreInZone3[10] = 1;

        int[] allCokeInZone1 = new int[ChargeDrop.StationCount];
        allCokeInZone1[2] = 1;
        allCokeInZone1[3] = 1;
        allCokeInZone1[4] = 1;

        ChargeProgram program = new(
        [
            new ChargeDrop(ChargeMaterial.Ore, allOreInZone3),
            new ChargeDrop(ChargeMaterial.Coke, allCokeInZone1),
        ]);

        MatrixStationConstraint constraint = new(program);
        double[] coords = constraint.Encode();
        ParameterSet result = constraint.Decode(ParameterSet.Defaults, coords);

        Assert.Equal(100.0, result[Parameter.ChargeStations9To11], 10);
        Assert.Equal(0.0, result[Parameter.ChargeStations3To5], 10);
    }

    [Fact]
    public void Decode_AtZeroCoords_PutsAllMovableInZone9To11()
    {
        // All zeros: each drop sends its entire free-zone allocation to zone 9-11.
        // WorkbookRight has movableOre/totalOre=18/18=1 and movableCoke/totalCoke=16/20=0.8,
        // so share9To11 = 1/(1+0.8) × 100 = 500/9 ≈ 55.6%.
        ChargeProgram program = WorkbookRight();
        MatrixStationConstraint constraint = new(program);
        ParameterSet result = constraint.Decode(ParameterSet.Defaults, new double[constraint.Dimensions]);

        Assert.Equal(500.0 / 9.0, result[Parameter.ChargeStations9To11], 10);
        Assert.Equal(0.0, result[Parameter.ChargeStations6To8], 10);
        Assert.Equal(0.0, result[Parameter.ChargeStations3To5], 10);
    }

    [Fact]
    public void Decode_AtOneZeroCoords_PutsAllMovableInZone3To5()
    {
        // Setting each drop's first broken-stick coordinate to 1.0 sends the entire
        // free-zone allocation of every drop to zone 3-5. Produces the same share as
        // the all-zeros case but in zone 3-5.
        ChargeProgram program = WorkbookRight();
        MatrixStationConstraint constraint = new(program);
        double[] coords = new double[constraint.Dimensions];
        for (int i = 0; i < coords.Length; i += 2)
        {
            coords[i] = 1.0;
        }

        ParameterSet result = constraint.Decode(ParameterSet.Defaults, coords);

        Assert.Equal(500.0 / 9.0, result[Parameter.ChargeStations3To5], 10);
        Assert.Equal(0.0, result[Parameter.ChargeStations6To8], 10);
        Assert.Equal(0.0, result[Parameter.ChargeStations9To11], 10);
    }

    [Fact]
    public void Decode_RandomCoords_AlwaysProducesValidPercentages()
    {
        ChargeProgram program = WorkbookRight();
        MatrixStationConstraint constraint = new(program);
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
        ChargeProgram program = new([]);
        MatrixStationConstraint constraint = new(program);
        Assert.False(constraint.IsMovable);
    }

    [Fact]
    public void IsMovable_AllPortionsInFixedZone_ReturnsFalse()
    {
        // All material in stations 1-2 (bucket 0 — the pinned zone).
        int[] portions = new int[ChargeDrop.StationCount];
        portions[0] = 3;
        portions[1] = 3;

        ChargeProgram program = new([
            new ChargeDrop(ChargeMaterial.Ore, portions),
            new ChargeDrop(ChargeMaterial.Coke, portions),
        ]);

        MatrixStationConstraint constraint = new(program);
        Assert.False(constraint.IsMovable);
    }

    [Fact]
    public void MatrixMode_OptimizerCanReachTargetBuiltFromIntegerRedistribution()
    {
        // A single ore drop and a single coke drop give 4 search dimensions — the
        // same size as the old aggregate encoding — so the standard budget is enough
        // to converge to machine precision. The target puts all movable material into
        // zone 9-11 (all-zeros coordinates); this is an integer-achievable point.
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
        MatrixStationConstraint constraint = new(program);

        ParameterSet startPs = constraint.Decode(ParameterSet.Defaults, constraint.Encode());
        ParameterSet targetPs = constraint.Decode(ParameterSet.Defaults, new double[constraint.Dimensions]);
        TemperatureProfile target = calculator.Calculate(targetPs);

        OptimizationRequest request = new()
        {
            Start = startPs,
            Target = target,
            FreeParameters = [],
            StationMode = new ChargeStationMode.Matrix(program),
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
