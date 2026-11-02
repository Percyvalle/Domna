using System.Globalization;
using Domna.Domain.Parameters;

namespace Domna.UI.ViewModels.Tests;

public class ParameterInputViewModelTests
{
    private readonly ParameterInputViewModel _input = new(
        ParameterLabels.All[(int)Parameter.ChargeStations9To11], () => { }, () => { }, () => { });

    public ParameterInputViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");

    [Theory]
    [InlineData(75.0, 73.0, "75,0 → 73,0")]
    [InlineData(75.0, 77.0, "75,0 → 77,0")]
    [InlineData(75.01, 75.02, "75,01 → 75,02")]
    public void OptimizerChangeShowsTheValuesBeforeAndAfter(double before, double after, string expected)
    {
        _input.ShowOptimizerChange(before, after);

        Assert.Equal(expected, _input.OptimizerChange);
    }

    [Fact]
    public void UnchangedParameterHasNoChangeCaption()
    {
        _input.ShowOptimizerChange(75.0, 73.0);
        _input.ShowOptimizerChange(73.0, 73.0);

        Assert.Empty(_input.OptimizerChange);
        Assert.Empty(_input.OptimizerChangeDetail);
    }

    [Fact]
    public void TooltipShowsTheDetailedValuesAndUnits()
    {
        _input.ShowOptimizerChange(75.1234, 73.5678);

        Assert.Equal("75,1 → 73,6", _input.OptimizerChange);
        Assert.Equal("До: 75,1234 %\nПосле: 73,5678 %", _input.OptimizerChangeDetail);
    }

    [Fact]
    public void ManualEditClearsThePreviousOptimizationChange()
    {
        _input.ShowOptimizerChange(75.0, 73.0);

        _input.Value = 76.0;

        Assert.Empty(_input.OptimizerChange);
        Assert.Empty(_input.OptimizerChangeDetail);
    }

    [Fact]
    public void ChangeCaptionsUseTheCurrentCulture()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

        _input.ShowOptimizerChange(75.1, 73.2);

        Assert.Equal("75.1 → 73.2", _input.OptimizerChange);
        Assert.Equal("До: 75.1 %\nПосле: 73.2 %", _input.OptimizerChangeDetail);
    }
}
