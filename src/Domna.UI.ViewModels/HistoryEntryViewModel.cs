using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domna.Domain.Models;

namespace Domna.UI.ViewModels;

/// <summary>
/// One saved calculation in the session history.
/// </summary>
/// <remarks>
/// The history lives in memory only: closing the app loses it. That is a property
/// of the product, not an omission — there is no store to write to, and a saved
/// calculation is a working comparison, not a record.
/// </remarks>
public sealed partial class HistoryEntryViewModel : ObservableObject
{
    private readonly Action<HistoryEntryViewModel> _onLoad;
    private readonly Action<HistoryEntryViewModel> _onCompare;
    private readonly Action<HistoryEntryViewModel> _onDelete;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompareCaption))]
    private bool _isBaseline;

    public HistoryEntryViewModel(
        int number,
        DateTimeOffset savedAt,
        ParameterSet parameters,
        TemperatureProfile profile,
        Action<HistoryEntryViewModel> onLoad,
        Action<HistoryEntryViewModel> onCompare,
        Action<HistoryEntryViewModel> onDelete,
        ChargeProgram? chargeProgram = null)
    {
        ArgumentNullException.ThrowIfNull(onLoad);
        ArgumentNullException.ThrowIfNull(onCompare);
        ArgumentNullException.ThrowIfNull(onDelete);

        Number = number;
        SavedAt = savedAt;
        Parameters = parameters;
        Profile = profile;
        ChargeProgram = chargeProgram;
        _onLoad = onLoad;
        _onCompare = onCompare;
        _onDelete = onDelete;
    }

    /// <summary>The calculation's number within this session.</summary>
    public int Number { get; }

    /// <summary>When the calculation was saved.</summary>
    public DateTimeOffset SavedAt { get; }

    public string Label => string.Create(
        CultureInfo.CurrentCulture,
        $"Snapshot #{Number:000} | {SavedAt:HH:mm:ss}");

    public ParameterSet Parameters { get; }

    public TemperatureProfile Profile { get; }

    public ChargeProgram? ChargeProgram { get; }

    public string Center => DisplayNumber.Fixed(Profile.Center, DisplayNumber.TemperatureDecimals);

    public string OreRidge => DisplayNumber.Fixed(Profile.OreRidge, DisplayNumber.TemperatureDecimals);

    public string Periphery => DisplayNumber.Fixed(Profile.Periphery, DisplayNumber.TemperatureDecimals);

    public string CompareCaption => IsBaseline ? "Снять базу" : "Сравнить";

    /// <summary>Puts this entry's parameters back into the input list and recalculates.</summary>
    [RelayCommand]
    public void Load() => _onLoad(this);

    /// <summary>Makes this entry the baseline every delta on the screen is read against.</summary>
    [RelayCommand]
    public void Compare() => _onCompare(this);

    /// <summary>Drops this entry from the session history.</summary>
    [RelayCommand]
    public void Delete() => _onDelete(this);
}
