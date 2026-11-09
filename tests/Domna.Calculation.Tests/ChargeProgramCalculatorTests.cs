using Domna.Domain.Models;

namespace Domna.Calculation.Tests;

public class ChargeProgramCalculatorTests
{
    private readonly ChargeProgramCalculator _calculator = new();

    [Theory]
    [InlineData(false, 100.0, 47.05882352941177, 42.5531914893617)]
    [InlineData(true, 68.96551724137932, 58.139534883720934, 42.5531914893617)]
    public void Calculate_MatchesTheWorkbookExamples(bool right, double zone9To11, double zone6To8, double zone3To5)
    {
        // Station totals from row 26 of both "Лист2" examples, in ascending station order.
        int[] coke = right
            ? [0, 4, 2, 2, 2, 3, 2, 3, 1, 1, 0]
            : [0, 4, 2, 2, 2, 4, 3, 3, 0, 0, 0];
        int[] ore = right
            ? [0, 0, 0, 1, 3, 4, 3, 3, 3, 1, 0]
            : [0, 0, 0, 1, 3, 3, 2, 3, 3, 3, 0];
        ChargeProgram program = new([new(ChargeMaterial.Coke, coke), new(ChargeMaterial.Ore, ore)]);

        ChargeStationShares result = Assert.IsType<ChargeStationShares>(_calculator.Calculate(program));

        Assert.Equal(zone9To11, result.Stations9To11, 12);
        Assert.Equal(zone6To8, result.Stations6To8, 12);
        Assert.Equal(zone3To5, result.Stations3To5, 12);
    }

    [Fact]
    public void Calculate_MovingOreBetweenStationsRecomputesBothAffectedGroups()
    {
        ChargeDrop coke = new(ChargeMaterial.Coke, [0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0]);
        ChargeDrop ore = new(ChargeMaterial.Ore, [0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0]);
        ChargeDrop moved = new(ChargeMaterial.Ore, [0, 0, 1, 0, 0, 0, 0, 0, 2, 0, 0]);

        ChargeStationShares before = Assert.IsType<ChargeStationShares>(_calculator.Calculate(new ChargeProgram([coke, ore])));
        ChargeStationShares after = Assert.IsType<ChargeStationShares>(_calculator.Calculate(new ChargeProgram([coke, moved])));

        Assert.Equal(50, before.Stations9To11, 12);
        Assert.Equal(200.0 / 3, after.Stations9To11, 12);
        Assert.Equal(0, after.Stations6To8, 12);
        Assert.Equal(before.Stations3To5, after.Stations3To5, 12);
    }

    [Fact]
    public void Calculate_UndefinedWithMissingMaterialOrEmptyGroup()
    {
        ChargeDrop coke = new(ChargeMaterial.Coke, [0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0]);
        ChargeDrop ore = new(ChargeMaterial.Ore, [0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0]);

        Assert.Null(_calculator.Calculate(new ChargeProgram([coke])));
        Assert.Null(_calculator.Calculate(new ChargeProgram([coke, ore])));
    }
}
