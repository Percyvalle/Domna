using System.Globalization;

namespace Domna.UI.ViewModels.Tests;

public class ValueDeltaTests
{
    public ValueDeltaTests()
    {
        // Display is a culture boundary: the operator this app is built for reads a
        // decimal comma, so the expected strings below are pinned to that culture
        // rather than to whatever locale the build agent happens to run under.
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
    }

    [Fact]
    public void WithoutABaseline_ThereIsNoDelta()
    {
        Assert.Equal(DeltaDirection.Unavailable, ValueDelta.Between(1.0, null, 3).Direction);
        Assert.Equal(DeltaDirection.Unavailable, ValueDelta.Between(null, 1.0, 3).Direction);
        Assert.Equal(string.Empty, ValueDelta.Between(null, null, 3).Text);
    }

    [Fact]
    public void ARiseIsSignedAndArrowed()
    {
        ValueDelta delta = ValueDelta.Between(285.0, 272.6, 1, " °C");

        Assert.Equal(DeltaDirection.Up, delta.Direction);
        Assert.Equal("↑ +12,4 °C", delta.Text);
    }

    [Fact]
    public void AFallIsSignedAndArrowed()
    {
        ValueDelta delta = ValueDelta.Between(1.900, 1.955, 3);

        Assert.Equal(DeltaDirection.Down, delta.Direction);
        Assert.Equal("↓ −0,055", delta.Text);
    }

    [Fact]
    public void ADifferenceBelowTheShownPrecisionIsReportedAsNoChange()
    {
        // 0.04 °C would round to "0,0" at one decimal: an arrow here would claim a
        // movement the displayed digits cannot show.
        ValueDelta delta = ValueDelta.Between(272.64, 272.60, 1, " °C");

        Assert.Equal(DeltaDirection.Unchanged, delta.Direction);
        Assert.Equal("— 0,0 °C", delta.Text);
    }
}
