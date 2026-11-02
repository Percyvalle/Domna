using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Domna.Domain.Parameters;

namespace Domna.UI.ViewModels;

/// <summary>
/// One editable process parameter: its value, its bounds, whether the value has
/// left the region the model was fitted in, and whether the optimizer may move it.
/// </summary>
public sealed partial class ParameterInputViewModel : ObservableObject
{
    private readonly Action _onValueChanged;
    private readonly Action _onFreeChanged;
    private readonly Action _onBoundsChanged;
    private (double Before, double After)? _optimizerChange;

    [ObservableProperty]
    private double _value;

    /// <summary>Whether the optimizer is allowed to move this parameter.</summary>
    [ObservableProperty]
    private bool _isFree;

    /// <summary>Lower bound used for out-of-range validation. Defaults to the catalogue minimum.</summary>
    [ObservableProperty]
    private double _effectiveMinimum;

    /// <summary>Upper bound used for out-of-range validation. Defaults to the catalogue maximum.</summary>
    [ObservableProperty]
    private double _effectiveMaximum;

    public ParameterInputViewModel(ParameterLabel label, Action onValueChanged, Action onFreeChanged, Action onBoundsChanged)
    {
        ArgumentNullException.ThrowIfNull(onValueChanged);
        ArgumentNullException.ThrowIfNull(onFreeChanged);
        ArgumentNullException.ThrowIfNull(onBoundsChanged);

        Parameter = label.Parameter;
        DisplayName = label.Name;
        ShortName = label.ShortName;
        Unit = label.Unit;
        Range = ParameterCatalog.RangeOf(label.Parameter);

        _onValueChanged = onValueChanged;
        _onFreeChanged = onFreeChanged;
        _onBoundsChanged = onBoundsChanged;
        _value = Range.Default;
        _effectiveMinimum = Range.Minimum;
        _effectiveMaximum = Range.Maximum;
    }

    public Parameter Parameter { get; }

    public string DisplayName { get; }

    public string ShortName { get; }

    public string Unit { get; }

    public ParameterRange Range { get; }

    public string OptimizerChange => _optimizerChange is { } change
        ? FormatChange(change.Before, change.After)
        : string.Empty;

    public string OptimizerChangeDetail => _optimizerChange is { } change
        ? string.Create(
            CultureInfo.CurrentCulture,
            $"До: {change.Before:G} {Unit}\nПосле: {change.After:G} {Unit}")
        : string.Empty;

    /// <summary>
    /// True when the value sits outside the effective bounds. The result is still
    /// computed and shown — extrapolation is the operator's call — but it is
    /// flagged, because the regression says nothing out here.
    /// </summary>
    public bool IsOutOfRange => Value < EffectiveMinimum || Value > EffectiveMaximum;

    public void ShowOptimizerChange(double before, double after)
    {
        _optimizerChange = double.IsFinite(before) && double.IsFinite(after) && Math.Abs(after - before) > 0.0
            ? (before, after)
            : null;
        NotifyOptimizerChange();
    }

    public void ClearOptimizerChange()
    {
        if (_optimizerChange is null)
        {
            return;
        }

        _optimizerChange = null;
        NotifyOptimizerChange();
    }

    partial void OnValueChanged(double value)
    {
        ClearOptimizerChange();
        OnPropertyChanged(nameof(IsOutOfRange));
        _onValueChanged();
    }

    partial void OnIsFreeChanged(bool value) => _onFreeChanged();

    partial void OnEffectiveMinimumChanged(double value)
    {
        OnPropertyChanged(nameof(IsOutOfRange));
        _onBoundsChanged();
    }

    partial void OnEffectiveMaximumChanged(double value)
    {
        OnPropertyChanged(nameof(IsOutOfRange));
        _onBoundsChanged();
    }

    private void NotifyOptimizerChange()
    {
        OnPropertyChanged(nameof(OptimizerChange));
        OnPropertyChanged(nameof(OptimizerChangeDetail));
    }

    private string FormatChange(double before, double after)
    {
        string start = DisplayNumber.Fixed(before, Range.Decimals);
        string result = DisplayNumber.Fixed(after, Range.Decimals);

        // Do not describe a real move as two identical rounded values.
        if (string.Equals(start, result, StringComparison.Ordinal))
        {
            start = before.ToString("G", CultureInfo.CurrentCulture);
            result = after.ToString("G", CultureInfo.CurrentCulture);
        }

        return start + " → " + result;
    }
}
