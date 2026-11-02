using System.Globalization;

namespace Domna.UI.ViewModels.Tests;

public class OptimizationTargetViewModelTests
{
    private readonly OptimizationTargetViewModel _target = new("Тц/Тгр", false);

    public OptimizationTargetViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");

    [Fact]
    public void WithoutABaseline_ThereIsNothingToClose()
    {
        _target.Show(null, 1.0);

        Assert.Equal("–", _target.Formatted);
        Assert.Equal("–", _target.DetailedDifference);
        Assert.False(_target.IsOpen);
        Assert.Equal(0.0, _target.AboveMagnitude);
        Assert.Equal(0.0, _target.BelowMagnitude);
    }

    [Fact]
    public void AGapIsAlwaysSigned()
    {
        _target.Show(0.084, 1.0);
        Assert.Equal("+0,0840", _target.Formatted);

        _target.Show(-0.641, 1.0);
        Assert.Equal("−0,6410", _target.Formatted);
    }

    [Fact]
    public void AGapBelowTheEmphasisThresholdRemainsVisible()
    {
        _target.Show(0.0004, 1.0);

        Assert.False(_target.IsOpen);
        Assert.Equal("+0,0004", _target.Formatted);
    }

    [Theory]
    [InlineData(0.00000001, "+0,00000001")]
    [InlineData(-0.00000001, "−0,00000001")]
    [InlineData(0.0000007, "+0,0000007")]
    [InlineData(-0.00009999, "−0,00009999")]
    [InlineData(double.Epsilon, "+5E-324")]
    public void TinyGapsShowAMagnitudeBoundAndASignedDetail(double difference, string expected)
    {
        _target.Show(difference, 1.0);

        Assert.Equal("< 0,0001", _target.Formatted);
        Assert.Equal(expected, _target.DetailedDifference);
        Assert.False(_target.IsOpen);
    }

    [Fact]
    public void ExactZeroUsesOrdinaryNotationWithoutASign()
    {
        _target.Show(0.0, 1.0);

        Assert.Equal("0,0000", _target.Formatted);
        Assert.Equal("0", _target.DetailedDifference);
        Assert.False(_target.IsOpen);
        Assert.Equal(0.0, _target.AboveMagnitude);
        Assert.Equal(0.0, _target.BelowMagnitude);
    }

    [Theory]
    [InlineData(0.0005)]
    [InlineData(-0.0005)]
    public void GapsAtTheEmphasisThresholdKeepOrdinaryNotation(double difference)
    {
        _target.Show(difference, 1.0);

        Assert.DoesNotContain("E", _target.Formatted);
        Assert.True(_target.IsOpen);
    }

    [Fact]
    public void GapAndDetailUseTheCurrentCulture()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

        _target.Show(0.0000000125, 1.0);

        Assert.Equal("< 0.0001", _target.Formatted);
        Assert.Equal("+0.0000000125", _target.DetailedDifference);
    }

    [Theory]
    [InlineData(0.0001, "+0,0001")]
    [InlineData(-0.0001, "−0,0001")]
    [InlineData(0.0009, "+0,0009")]
    [InlineData(0.08412, "+0,0841")]
    public void GapsAtOrAboveTheDisplayThresholdUseFourDecimals(double difference, string expected)
    {
        _target.Show(difference, 1.0);

        Assert.Equal(expected, _target.Formatted);
    }

    [Fact]
    public void DetailRetainsDigitsBeyondTheCardPrecision()
    {
        _target.Show(0.08412, 1.0);

        Assert.Equal("+0,08412", _target.DetailedDifference);
    }

    [Fact]
    public void ChangingTheGapNotifiesTheTooltip()
    {
        List<string?> changed = [];
        _target.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        _target.Show(0.0000007, 1.0);

        Assert.Contains(nameof(OptimizationTargetViewModel.DetailedDifference), changed);
    }

    [Fact]
    public void TheBarGrowsToTheSideTheGapIsOn()
    {
        _target.Show(0.25, 0.5);
        Assert.Equal(50.0, _target.AboveMagnitude, 1e-9);
        Assert.Equal(0.0, _target.BelowMagnitude);

        _target.Show(-0.5, 0.5);
        Assert.Equal(0.0, _target.AboveMagnitude);
        Assert.Equal(100.0, _target.BelowMagnitude, 1e-9);
    }

    [Fact]
    public void TheBarNeverOverflowsItsScale()
    {
        _target.Show(4.0, 0.5);

        Assert.Equal(100.0, _target.AboveMagnitude);
    }
}
