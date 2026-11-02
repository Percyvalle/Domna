using CommunityToolkit.Mvvm.ComponentModel;
using Domna.Domain.Models;

namespace Domna.UI.ViewModels;

/// <summary>
/// One radial measuring position, as the operator reads it: the temperature and
/// how far it sits from the baseline calculation.
/// </summary>
public sealed partial class TemperatureZoneViewModel : ObservableObject
{
    private double? _celsius;
    private double? _baselineCelsius;

    public TemperatureZoneViewModel(TemperaturePosition position, string displayName)
    {
        Position = position;
        DisplayName = displayName;
    }

    public TemperaturePosition Position { get; }

    public string DisplayName { get; }

    /// <summary>The temperature, or <see langword="null"/> before the first calculation.</summary>
    public double? Celsius => _celsius;

    public string Formatted => DisplayNumber.Temperature(_celsius);

    public ValueDelta Delta =>
        ValueDelta.Between(_celsius, _baselineCelsius, DisplayNumber.TemperatureDecimals, " °C");

    /// <summary>Points the zone at a new result and a new baseline, either of which may be absent.</summary>
    public void Show(TemperatureProfile? profile, TemperatureProfile? baseline)
    {
        _celsius = profile?.At(Position);
        _baselineCelsius = baseline?.At(Position);

        OnPropertyChanged(nameof(Celsius));
        OnPropertyChanged(nameof(Formatted));
        OnPropertyChanged(nameof(Delta));
    }
}
