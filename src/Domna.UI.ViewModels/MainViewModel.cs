using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domna.Domain.Interfaces;
using Domna.Domain.Models;
using Domna.Domain.Parameters;

namespace Domna.UI.ViewModels;

/// <summary>
/// The dashboard: process parameters on the left, the resulting profile and the
/// optimizer in the middle, the session's saved calculations on the right.
/// </summary>
/// <remarks>
/// Recalculation is a deliberate act, not a side effect of typing. The screen keeps
/// showing the last result and says out loud that the input has moved past it, so
/// the operator never reads a number that is halfway between two parameter sets.
/// </remarks>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>
    /// Fit quality, as carried by the reference implementation.
    /// </summary>
    /// <remarks>
    /// Shown because a prediction without a measure of trust is either overrated or
    /// ignored. Which furnace and which period the 336 observations come from is an
    /// open question — see <c>docs/ui-design-inputs.md</c> §4.3.
    /// </remarks>
    public const string ModelQualityCaption =
        "R²(Тц) = 0.854 · R²(Тц/Тгр) = 0.842\nR²(Тпер/Тц) = 0.785 · N = 336";

    private readonly ITemperatureProfileCalculator _calculator;
    private readonly IHistoryExportTarget _export;
    private readonly TimeProvider _time;

    private ParameterSet? _committed;
    private TemperatureProfile? _profile;
    private HistoryEntryViewModel? _baseline;
    private bool _applyingParameters;
    private int _sequence = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLine))]
    [NotifyCanExecuteChangedFor(nameof(OpenProtocolCommand))]
    private bool _isStale;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProtocolOpen))]
    private ProtocolViewModel? _protocol;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCalculationError))]
    private string? _calculationErrorDetail;

    /// <summary>Where the history was last exported, shown until the history changes.</summary>
    [ObservableProperty]
    private string? _lastExportPath;

    public MainViewModel(
        ITemperatureProfileCalculator calculator,
        IParameterOptimizer optimizer,
        IHistoryExportTarget exportTarget,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(optimizer);
        ArgumentNullException.ThrowIfNull(exportTarget);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _calculator = calculator;
        _export = exportTarget;
        _time = timeProvider;

        Inputs = [.. ParameterLabels.All.Select(
            label => new ParameterInputViewModel(label, OnInputEdited, OnFreeParametersChanged, OnBoundsChanged))];

        // The optimizer panel is owned here rather than injected, because the only
        // thing it can do with an answer is hand it back to this input list.
        Optimization = new OptimizationViewModel(optimizer, ApplyOptimizerResult);

        RefreshOptimization();
    }

    public ObservableCollection<ParameterInputViewModel> Inputs { get; }

    public ObservableCollection<HistoryEntryViewModel> History { get; } = [];

    public OptimizationViewModel Optimization { get; }

    /// <summary>The three ratios, in screen order: they lead, the temperatures follow.</summary>
    public IReadOnlyList<RatioReadingViewModel> Ratios { get; } =
    [
        new("Тц/Тгр", TemperaturePosition.Center, TemperaturePosition.OreRidge),
        new("Тпер/Тц", TemperaturePosition.Periphery, TemperaturePosition.Center),
        new("Тпер/Тгр", TemperaturePosition.Periphery, TemperaturePosition.OreRidge),
    ];

    /// <summary>The three measuring positions, from the axis outwards.</summary>
    public IReadOnlyList<TemperatureZoneViewModel> Zones { get; } =
    [
        new(TemperaturePosition.Center, "Тц · центр"),
        new(TemperaturePosition.OreRidge, "Тгр · рудный гребень"),
        new(TemperaturePosition.Periphery, "Тпер · периферия"),
    ];

    /// <summary>The profile on screen, or <see langword="null"/> before the first calculation.</summary>
    public TemperatureProfile? Profile => _profile;

    /// <summary>The profile every delta is read against, or <see langword="null"/> when no baseline is set.</summary>
    public TemperatureProfile? BaselineProfile => _baseline?.Profile;

    public bool HasResult => _profile is not null;

    public bool IsHistoryEmpty => History.Count == 0;

    /// <summary>True while there is at least one saved calculation to export.</summary>
    public bool HasHistory => History.Count > 0;

    public bool IsProtocolOpen => Protocol is not null;

    public bool HasCalculationError => CalculationErrorDetail is not null;

    /// <summary>True while any input sits outside the region the model was fitted in.</summary>
    public bool HasOutOfRangeInputs => Inputs.Any(input => input.IsOutOfRange);

    public string FreeParametersCaption => string.Create(
        CultureInfo.CurrentCulture,
        $"свободных: {Inputs.Count(input => input.IsFree)} / {ParameterCatalog.Count}");

    public string StatusLine => !HasResult
        ? "Параметры введены, расчёт не запускался — нажмите «Рассчитать»"
        : IsStale
            ? "Ввод изменён — показанный результат устарел"
            : "Расчёт актуален · пересчёт по кнопке";

    private ParameterSet CurrentParameters =>
        ParameterSet.From(Inputs.ToDictionary(input => input.Parameter, input => input.Value));

    /// <summary>Runs the model on what is currently in the input list.</summary>
    [RelayCommand]
    public void Calculate()
    {
        ParameterSet parameters = CurrentParameters;
        TemperatureProfile profile = _calculator.Calculate(parameters);

        if (!profile.IsFinite)
        {
            // Keep the previous result on screen rather than replacing it with a
            // non-number, and say plainly that it belongs to the previous input.
            CalculationErrorDetail = DescribeNonFiniteResult(parameters);
            return;
        }

        _committed = parameters;
        _profile = profile;
        IsStale = false;
        CalculationErrorDetail = null;

        RefreshResult();
    }

    /// <summary>Returns every parameter to its catalogue default and recalculates.</summary>
    [RelayCommand]
    public void Reset() => Apply(ParameterSet.Defaults, ClearOptimizerChanges);

    /// <summary>Puts the inputs back where the last successful calculation left them.</summary>
    [RelayCommand]
    public void RestoreLastInput()
    {
        CalculationErrorDetail = null;

        if (_committed is { } parameters)
        {
            Apply(parameters, ClearOptimizerChanges);
        }
    }

    /// <summary>Calculates what is entered, if needed, and saves it as a baseline.</summary>
    /// <remarks>
    /// Saving runs the calculation first when the input has moved past the result on
    /// screen. Asking to save is asking to keep the parameters the operator is
    /// looking at, and making them press two buttons to do it only invites saving
    /// the wrong one of the two.
    /// </remarks>
    [RelayCommand]
    public void SaveSnapshot()
    {
        if (IsStale || !HasResult)
        {
            Calculate();
        }

        // Still stale means the calculation failed and said so; there is nothing
        // here worth keeping as a baseline.
        if (IsStale || _committed is not { } parameters || _profile is not { } profile)
        {
            return;
        }

        HistoryEntryViewModel entry = new(
            _sequence, _time.GetLocalNow(), parameters, profile, LoadEntry, ToggleBaseline, RemoveEntry);
        History.Add(entry);
        _sequence++;

        NotifyHistoryChanged();

        // The first saved calculation becomes the baseline: without one, every delta
        // on the screen would stay blank and the optimizer would have nothing to aim at.
        if (_baseline is null)
        {
            ToggleBaseline(entry);
        }
        else
        {
            RefreshResult();
        }
    }

    /// <summary>Drops the session history and the baseline with it.</summary>
    [RelayCommand]
    public void ClearHistory()
    {
        History.Clear();
        _baseline = null;

        NotifyHistoryChanged();
        RefreshResult();
    }

    /// <summary>Writes the whole session history to a spreadsheet table.</summary>
    /// <remarks>
    /// The table is written in the operator's own culture so that Excel on their
    /// machine reads the numbers as numbers — see <see cref="HistoryTable"/>.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasHistory))]
    public void ExportHistory()
    {
        if (History.Count == 0)
        {
            return;
        }

        string suggestedName = string.Create(
            CultureInfo.CurrentCulture,
            $"Домна · история {_time.GetLocalNow():yyyy-MM-dd HH-mm}{HistoryTable.FileExtension}");

        LastExportPath = _export.Write(
            suggestedName,
            HistoryTable.Render(History, CultureInfo.CurrentCulture));
    }

    /// <summary>Freezes the calculation on screen into a printable protocol.</summary>
    [RelayCommand(CanExecute = nameof(HasFreshResult))]
    public void OpenProtocol()
    {
        if (_profile is { } profile)
        {
            Protocol = ProtocolViewModel.Of(Inputs, profile, _sequence, _time.GetLocalNow());
        }
    }

    /// <summary>Closes the protocol sheet.</summary>
    [RelayCommand]
    public void CloseProtocol() => Protocol = null;

    /// <summary>Dismisses the failed-calculation notice, leaving the inputs untouched.</summary>
    [RelayCommand]
    public void DismissCalculationError() => CalculationErrorDetail = null;

    private bool HasFreshResult() => HasResult && !IsStale;

    /// <summary>Republishes everything that depends on the history's contents.</summary>
    /// <remarks>
    /// The last export path is dropped here: once a calculation is added or removed
    /// the file on disk no longer describes the history, and a stale confirmation is
    /// worse than none.
    /// </remarks>
    private void NotifyHistoryChanged()
    {
        LastExportPath = null;

        OnPropertyChanged(nameof(IsHistoryEmpty));
        OnPropertyChanged(nameof(HasHistory));

        ExportHistoryCommand.NotifyCanExecuteChanged();
    }

    private void OnInputEdited()
    {
        if (_applyingParameters)
        {
            return;
        }

        IsStale = true;

        OnPropertyChanged(nameof(HasOutOfRangeInputs));
    }

    private void OnFreeParametersChanged()
    {
        OnPropertyChanged(nameof(FreeParametersCaption));
        RefreshOptimization();
    }

    private void OnBoundsChanged()
    {
        OnPropertyChanged(nameof(HasOutOfRangeInputs));
    }

    /// <summary>Writes a parameter set into the input list as one edit, then recalculates.</summary>
    private void Apply(ParameterSet parameters, Action afterWrite)
    {
        _applyingParameters = true;
        try
        {
            foreach (ParameterInputViewModel input in Inputs)
            {
                input.Value = parameters[input.Parameter];
            }
        }
        finally
        {
            _applyingParameters = false;
        }

        afterWrite();

        OnPropertyChanged(nameof(HasOutOfRangeInputs));
        Calculate();
    }

    private void ClearOptimizerChanges()
    {
        foreach (ParameterInputViewModel input in Inputs)
        {
            input.ClearOptimizerChange();
        }
    }

    /// <summary>Takes the optimizer's answer and shows the transition from its starting values.</summary>
    private void ApplyOptimizerResult(ParameterSet before, ParameterSet found)
    {
        Apply(found, () =>
        {
            foreach (ParameterInputViewModel input in Inputs)
            {
                input.ShowOptimizerChange(before[input.Parameter], found[input.Parameter]);
            }
        });
    }

    private void LoadEntry(HistoryEntryViewModel entry) => Apply(entry.Parameters, ClearOptimizerChanges);

    /// <summary>Drops one saved calculation, and the baseline with it if that is what it was.</summary>
    /// <remarks>
    /// Removing the baseline does not promote another entry to take its place. Which
    /// calculation the deltas are read against is the operator's choice, and picking a
    /// replacement for them would silently change every number on the screen.
    /// </remarks>
    private void RemoveEntry(HistoryEntryViewModel entry)
    {
        History.Remove(entry);

        if (ReferenceEquals(_baseline, entry))
        {
            _baseline = null;
        }

        NotifyHistoryChanged();
        RefreshResult();
    }

    private void ToggleBaseline(HistoryEntryViewModel entry)
    {
        bool becomesBaseline = !entry.IsBaseline;

        foreach (HistoryEntryViewModel other in History)
        {
            other.IsBaseline = becomesBaseline && ReferenceEquals(other, entry);
        }

        _baseline = becomesBaseline ? entry : null;

        RefreshResult();
    }

    private void RefreshResult()
    {
        TemperatureProfile? baseline = BaselineProfile;

        foreach (RatioReadingViewModel ratio in Ratios)
        {
            ratio.Show(_profile, baseline);
        }

        foreach (TemperatureZoneViewModel zone in Zones)
        {
            zone.Show(_profile, baseline);
        }

        OnPropertyChanged(nameof(Profile));
        OnPropertyChanged(nameof(BaselineProfile));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(HasOutOfRangeInputs));
        OnPropertyChanged(nameof(StatusLine));

        OpenProtocolCommand.NotifyCanExecuteChanged();

        RefreshOptimization();
    }

    private void RefreshOptimization() => Optimization.Refresh(
        _committed ?? CurrentParameters,
        _profile,
        BaselineProfile,
        [.. Inputs.Where(input => input.IsFree).Select(input => input.Parameter)],
        BaselinePairCaption());

    private string BaselinePairCaption()
    {
        if (_baseline is null)
        {
            return string.Empty;
        }

        string label = _baseline.Label.Split(" |")[0];
        return string.Create(CultureInfo.CurrentCulture, $"{label} → Расчёт #{_sequence:000}");
    }

    private string DescribeNonFiniteResult(ParameterSet parameters)
    {
        ParameterInputViewModel? culprit = Inputs.FirstOrDefault(input => input.IsOutOfRange);

        string where = culprit is null
            ? "все параметры внутри области обучения"
            : string.Create(
                CultureInfo.CurrentCulture,
                $"{culprit.ShortName} = {DisplayNumber.Fixed(parameters[culprit.Parameter], culprit.Range.Decimals)}");

        return $"DomainError: non-finite profile · {where}";
    }
}
