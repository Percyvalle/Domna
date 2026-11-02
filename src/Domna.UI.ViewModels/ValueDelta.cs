namespace Domna.UI.ViewModels;

/// <summary>
/// The difference between a computed value and the baseline it is read against.
/// </summary>
/// <remarks>
/// A difference smaller than the precision the value is shown with is reported as
/// <see cref="DeltaDirection.Unchanged"/>, not as a tiny arrow: an operator must not
/// see a change that the displayed digits do not support.
/// </remarks>
/// <param name="Direction">Which way the value moved, if it moved at all.</param>
/// <param name="Text">The delta as the operator reads it, e.g. "↑ +12,3 °C".</param>
public readonly record struct ValueDelta(DeltaDirection Direction, string Text)
{
    /// <summary>No baseline to compare against.</summary>
    public static ValueDelta None { get; } = new(DeltaDirection.Unavailable, string.Empty);

    /// <summary>The delta of <paramref name="value"/> against <paramref name="baseline"/>.</summary>
    /// <param name="value">The computed value, or <see langword="null"/> when there is none.</param>
    /// <param name="baseline">The baseline value, or <see langword="null"/> when no baseline is set.</param>
    /// <param name="decimals">Decimals the value is displayed with — also the threshold.</param>
    /// <param name="unit">Unit suffix, already including its leading space.</param>
    public static ValueDelta Between(double? value, double? baseline, int decimals, string unit = "")
    {
        if (value is not { } actual || baseline is not { } reference)
        {
            return None;
        }

        double difference = actual - reference;
        double threshold = Math.Pow(10.0, -decimals) / 2.0;

        if (Math.Abs(difference) < threshold)
        {
            return new ValueDelta(DeltaDirection.Unchanged, "— " + DisplayNumber.Fixed(0.0, decimals) + unit);
        }

        string magnitude = DisplayNumber.Fixed(Math.Abs(difference), decimals) + unit;
        return difference > 0.0
            ? new ValueDelta(DeltaDirection.Up, "↑ +" + magnitude)
            : new ValueDelta(DeltaDirection.Down, "↓ −" + magnitude);
    }
}
