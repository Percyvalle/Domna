using CommunityToolkit.Mvvm.ComponentModel;
using Domna.Domain.Models;

namespace Domna.UI.ViewModels;

/// <summary>
/// One temperature ratio, as the operator reads it.
/// </summary>
/// <remarks>
/// The ratios lead the screen because they describe the shape of the gas flow,
/// which is what the charge program steers; the absolute temperatures below them
/// mostly follow the heat balance.
/// </remarks>
public sealed partial class RatioReadingViewModel : ObservableObject
{
    private readonly TemperaturePosition _numerator;
    private readonly TemperaturePosition _denominator;

    private double? _value;
    private double? _baseline;

    public RatioReadingViewModel(string label, TemperaturePosition numerator, TemperaturePosition denominator)
    {
        Label = label;
        _numerator = numerator;
        _denominator = denominator;
    }

    public string Label { get; }

    /// <summary>The ratio, or <see langword="null"/> before the first calculation.</summary>
    public double? Value => _value;

    public string Formatted => DisplayNumber.Ratio(_value);

    public ValueDelta Delta => ValueDelta.Between(_value, _baseline, DisplayNumber.RatioDecimals);

    /// <summary>Points the reading at a new result and a new baseline, either of which may be absent.</summary>
    public void Show(TemperatureProfile? profile, TemperatureProfile? baseline)
    {
        _value = profile?.Ratio(_numerator, _denominator);
        _baseline = baseline?.Ratio(_numerator, _denominator);

        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(Formatted));
        OnPropertyChanged(nameof(Delta));
    }
}
