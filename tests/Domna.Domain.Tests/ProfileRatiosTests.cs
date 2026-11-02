using Domna.Domain.Models;

namespace Domna.Domain.Tests;

public class ProfileRatiosTests
{
    [Fact]
    public void Of_ReadsTheThreeRatiosOffTheProfile()
    {
        TemperatureProfile profile = new(272.6, 139.4, 170.4);

        ProfileRatios ratios = ProfileRatios.Of(profile);

        Assert.Equal(272.6 / 139.4, ratios.CenterToOreRidge, 1e-12);
        Assert.Equal(170.4 / 272.6, ratios.PeripheryToCenter, 1e-12);
        Assert.Equal(170.4 / 139.4, ratios.PeripheryToOreRidge, 1e-12);
    }

    [Fact]
    public void Of_ADegenerateProfile_ReportsZeroRatherThanAnInfinity()
    {
        ProfileRatios ratios = ProfileRatios.Of(new TemperatureProfile(200.0, 0.0, 100.0));

        Assert.Equal(0.0, ratios.CenterToOreRidge);
        Assert.Equal(0.0, ratios.PeripheryToOreRidge);
    }

    [Theory]
    [InlineData(double.NaN, 1.0, 1.0)]
    [InlineData(1.0, double.PositiveInfinity, 1.0)]
    [InlineData(1.0, 1.0, double.NegativeInfinity)]
    public void IsFinite_RejectsAProfileThatLeftTheReals(double center, double oreRidge, double periphery)
    {
        Assert.False(new TemperatureProfile(center, oreRidge, periphery).IsFinite);
    }

    [Fact]
    public void IsFinite_AcceptsAnOrdinaryProfile()
    {
        Assert.True(new TemperatureProfile(272.6, 139.4, 170.4).IsFinite);
    }
}
