using Domna.Domain.Models;

namespace Domna.Domain.Tests;

public class TemperatureProfileTests
{
    [Fact]
    public void At_ReturnsTheTemperatureOfEachPosition()
    {
        TemperatureProfile profile = new(270.0, 140.0, 170.0);

        Assert.Equal(270.0, profile.At(TemperaturePosition.Center));
        Assert.Equal(140.0, profile.At(TemperaturePosition.OreRidge));
        Assert.Equal(170.0, profile.At(TemperaturePosition.Periphery));
    }

    [Fact]
    public void Ratio_DividesTheTwoPositions()
    {
        TemperatureProfile profile = new(270.0, 135.0, 170.0);

        Assert.Equal(2.0, profile.Ratio(TemperaturePosition.Center, TemperaturePosition.OreRidge));
    }

    [Fact]
    public void Ratio_ReturnsZeroRatherThanInfinityOnAZeroDenominator()
    {
        TemperatureProfile degenerate = new(270.0, 0.0, 170.0);

        Assert.Equal(0.0, degenerate.Ratio(TemperaturePosition.Center, TemperaturePosition.OreRidge));
    }

    [Fact]
    public void Ratio_ChainsThroughTheCentre()
    {
        // Tper/Tgr = (Tper/Tc) * (Tc/Tgr) — the identity the operator reads the
        // three ratios by; it holds because the ratios are derived, never stored.
        TemperatureProfile profile = new(272.59362591618179, 139.41992678663084, 170.40870586815851);

        double peripheryToRidge = profile.Ratio(TemperaturePosition.Periphery, TemperaturePosition.OreRidge);
        double chained = profile.Ratio(TemperaturePosition.Periphery, TemperaturePosition.Center)
                       * profile.Ratio(TemperaturePosition.Center, TemperaturePosition.OreRidge);

        Assert.Equal(chained, peripheryToRidge, 1e-12);
    }

    [Fact]
    public void At_RejectsAnUnknownPosition()
    {
        TemperatureProfile profile = new(270.0, 140.0, 170.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => profile.At((TemperaturePosition)99));
    }
}
