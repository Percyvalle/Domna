using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Domna.UI.ViewModels;

/// <summary>
/// One ratio the optimizer is driving to zero: how far the current calculation
/// still sits from the baseline it is being matched to.
/// </summary>
public sealed partial class OptimizationTargetViewModel : ObservableObject
{
    /// <summary>Threshold for visual emphasis; smaller nonzero gaps remain visible.</summary>
    private const double ClosedGap = 5e-4;

    private const double DisplayThreshold = 1e-4;
    private const int GapDecimals = 4;

    private double? _difference;
    private double _scale = 1.0;

    public OptimizationTargetViewModel(string label, bool isDerived)
    {
        Label = label;
        IsDerived = isDerived;
    }

    public string Label { get; }

    /// <summary>
    /// True for Тпер/Тгр, which the regression does not fit — it follows from the
    /// other two. The screen states it quietly for that reason: closing it is not a
    /// separate goal, it is what happens when the other two close.
    /// </summary>
    public bool IsDerived { get; }

    /// <summary>The signed gap, or a magnitude bound for tiny nonzero gaps.</summary>
    public string Formatted => _difference is { } difference
        ? FormatDifference(difference)
        : DisplayNumber.Missing;

    public string DetailedDifference => _difference is { } difference
        ? Sign(difference) + Math.Abs(difference).ToString(
            Math.Abs(difference) >= 1e-12 ? "0.###############" : "G",
            CultureInfo.CurrentCulture)
        : DisplayNumber.Missing;

    /// <summary>True while the gap is wide enough to be worth closing.</summary>
    public bool IsOpen => _difference is { } difference && Math.Abs(difference) >= ClosedGap;

    /// <summary>How far the bar fills above the zero mark, 0 to 100.</summary>
    public double AboveMagnitude => _difference > 0.0 ? Magnitude : 0.0;

    /// <summary>How far the bar fills below the zero mark, 0 to 100.</summary>
    public double BelowMagnitude => _difference < 0.0 ? Magnitude : 0.0;

    private double Magnitude =>
        _difference is { } difference && _scale > 0.0
            ? Math.Min(100.0, Math.Abs(difference) / _scale * 100.0)
            : 0.0;

    /// <summary>Points the target at a new gap, or at none.</summary>
    /// <param name="difference">The gap against the baseline, or <see langword="null"/>.</param>
    /// <param name="scale">The gap that fills the bar — shared by all three, so the
    /// bars are read against each other rather than each against itself.</param>
    public void Show(double? difference, double scale)
    {
        _difference = difference;
        _scale = scale;

        OnPropertyChanged(nameof(Formatted));
        OnPropertyChanged(nameof(DetailedDifference));
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(AboveMagnitude));
        OnPropertyChanged(nameof(BelowMagnitude));
    }

    private static string FormatDifference(double difference)
    {
        double magnitude = Math.Abs(difference);

        // This is a bound on magnitude, including negative gaps; the tooltip
        // retains the sign and more digits. It does not define convergence.
        if (magnitude > 0.0 && magnitude < DisplayThreshold)
        {
            return "< " + DisplayNumber.Fixed(DisplayThreshold, GapDecimals);
        }

        return Sign(difference) + DisplayNumber.Fixed(magnitude, GapDecimals);
    }

    private static string Sign(double difference) => difference switch
    {
        > 0.0 => "+",
        < 0.0 => "−",
        _ => string.Empty,
    };
}
