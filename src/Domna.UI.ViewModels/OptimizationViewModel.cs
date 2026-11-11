using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domna.Domain.Interfaces;
using Domna.Domain.Models;
using Domna.Domain.Optimization;
using Domna.Domain.Parameters;

namespace Domna.UI.ViewModels;

/// <summary>
/// The inverse problem on screen: which of the eleven parameters the optimizer may
/// move, how far the current calculation still sits from the baseline, and the
/// search itself.
/// </summary>
/// <remarks>
/// The search runs off the calling thread, so the operator can keep reading the
/// screen and can cancel. This class stays free of any windowing type.
/// </remarks>
public sealed partial class OptimizationViewModel : ObservableObject
{
    private const string NoBaselinePair = "база не выбрана";

    /// <summary>
    /// The smallest gap that still fills the bars completely.
    /// </summary>
    /// <remarks>
    /// Without a floor, three gaps of a thousandth each would draw three full bars
    /// and read as a wide miss. With it, a closed set of targets draws almost nothing,
    /// which is what "there is nothing left to close" should look like.
    /// </remarks>
    private const double MinimumBarScale = 0.02;

    private readonly IParameterOptimizer _optimizer;
    private readonly Action<ParameterSet, ParameterSet, ChargeProgram?> _apply;

    private ParameterSet _start = ParameterSet.Defaults;
    private TemperatureProfile? _baseline;
    private Parameter[] _free = [];
    private ChargeProgram? _program;
    private CancellationTokenSource? _cancellation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonCaption))]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private bool _isRunning;

    [ObservableProperty]
    private string _pairCaption = NoBaselinePair;

    /// <summary>
    /// When true, the optimizer moves integer portions between charge zones rather
    /// than adjusting the share values as continuous variables. Requires a charge
    /// program to have been entered; resets to false automatically when the program
    /// is cleared.
    /// </summary>
    [ObservableProperty]
    private bool _useMatrixMode;

    /// <summary>
    /// Selects the redistribution strategy within matrix mode.
    /// True: aggregate — ore and coke are pooled across all drops, so the optimizer
    /// can effectively transfer portions between drops of the same material type.
    /// False: per-drop — each drop redistributes its own portions independently.
    /// Has no effect when <see cref="UseMatrixMode"/> is false.
    /// </summary>
    [ObservableProperty]
    private bool _useAggregateMatrixMode = true;

    /// <summary>Mutation scaling factor (DE parameter F) passed to the next search.</summary>
    [ObservableProperty]
    private double _mutationWeight = 0.6;

    public OptimizationViewModel(IParameterOptimizer optimizer, Action<ParameterSet, ParameterSet, ChargeProgram?> apply)
    {
        ArgumentNullException.ThrowIfNull(optimizer);
        ArgumentNullException.ThrowIfNull(apply);

        _optimizer = optimizer;
        _apply = apply;
    }

    /// <summary>The three ratio gaps the search is closing, in screen order.</summary>
    public IReadOnlyList<OptimizationTargetViewModel> Targets { get; } =
    [
        new("Тц/Тгр", false),
        new("Тпер/Тц", false),
        new("Тпер/Тгр", true),
    ];

    public string ButtonCaption => IsRunning ? "Отменить" : "Запустить подбор";

    /// <summary>True when a charge program is available, enabling the matrix-mode toggle.</summary>
    public bool HasProgram => _program is not null;

    /// <summary>Decimal places used when displaying F in the UI.</summary>
    public int SearchSettingDecimals { get; } = 3;

    /// <summary>
    /// Re-points the panel at the current state of the screen.
    /// </summary>
    /// <param name="start">The parameter set the search would start from.</param>
    /// <param name="current">The profile currently on screen, or none.</param>
    /// <param name="baseline">The baseline profile every delta is read against, or none.</param>
    /// <param name="free">The parameters the operator has released to the optimizer.</param>
    /// <param name="pairCaption">Which two calculations the search runs between.</param>
    /// <param name="program">
    /// The charge program the current calculation was derived from, or <see langword="null"/>
    /// when the station values were entered manually. Supplying a program enables matrix mode.
    /// </param>
    public void Refresh(
        ParameterSet start,
        TemperatureProfile? current,
        TemperatureProfile? baseline,
        IReadOnlyList<Parameter> free,
        string pairCaption,
        ChargeProgram? program = null)
    {
        ArgumentNullException.ThrowIfNull(free);

        _start = start;
        _baseline = baseline;
        _free = [.. free];
        _program = program;

        if (program is null)
        {
            UseMatrixMode = false;
        }

        OnPropertyChanged(nameof(HasProgram));

        ProfileRatios? actual = current is { } profile ? ProfileRatios.Of(profile) : null;
        ProfileRatios? reference = baseline is { } target ? ProfileRatios.Of(target) : null;

        double?[] gaps =
        [
            Gap(actual?.CenterToOreRidge, reference?.CenterToOreRidge),
            Gap(actual?.PeripheryToCenter, reference?.PeripheryToCenter),
            Gap(actual?.PeripheryToOreRidge, reference?.PeripheryToOreRidge),
        ];

        // One scale for all three bars: the panel is read for which gap is the wide
        // one, and three separately scaled bars would hide exactly that.
        double scale = Math.Max(MinimumBarScale, gaps.Max(gap => Math.Abs(gap ?? 0.0)));

        for (int i = 0; i < Targets.Count; i++)
        {
            Targets[i].Show(gaps[i], scale);
        }

        PairCaption = baseline is null ? NoBaselinePair : pairCaption;

        RunCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Starts the search, or cancels the one already running.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    public void Run()
    {
        if (IsRunning)
        {
            _cancellation?.Cancel();
            return;
        }

        _ = SearchAsync();
    }

    private static double? Gap(double? actual, double? reference) =>
        actual is { } a && reference is { } b ? a - b : null;

    private static bool IsStationParameter(Parameter p) => p is
        Parameter.ChargeStations3To5 or Parameter.ChargeStations6To8 or Parameter.ChargeStations9To11;

    private bool CanRun()
    {
        if (IsRunning)
        {
            return true;
        }

        if (_baseline is null || _free.Length == 0)
        {
            return false;
        }

        // A single station parameter cannot be moved without a partner to absorb the
        // balance — ChargeStationBalance requires ≥2, and in matrix mode a single free
        // zone leaves no redistribution dimensions. Non-station parameters are always
        // effective.
        int stationCount = _free.Count(IsStationParameter);
        return stationCount >= 2 || stationCount < _free.Length;
    }

    private async Task SearchAsync()
    {
        MatrixOptimizerMode matrixMode = UseAggregateMatrixMode
            ? MatrixOptimizerMode.Aggregate
            : MatrixOptimizerMode.PerDrop;

        OptimizationRequest request = new()
        {
            Start = _start,
            Target = _baseline!.Value,
            FreeParameters = _free,
            Settings = new SearchSettings { MutationWeight = MutationWeight },
            StationMode = UseMatrixMode && _program is not null
                ? new ChargeStationMode.Matrix(_program, matrixMode)
                : new ChargeStationMode.Continuous(),
        };

        using CancellationTokenSource cancellation = new();
        _cancellation = cancellation;
        IsRunning = true;

        try
        {
            // No progress callback: the panel shows that a search is running, not how
            // far along it is. Round-by-round progress stays on the optimizer's
            // interface for whoever wants to put it back on screen.
            OptimizationOutcome outcome = await Task.Run(
                () => _optimizer.Optimize(request, null, cancellation.Token),
                CancellationToken.None).ConfigureAwait(true);

            // A cancelled search is not a result: its point must not reach the inputs.
            if (!outcome.Cancelled)
            {
                _apply(request.Start, outcome.Parameters, outcome.UpdatedChargeProgram);
            }
        }
        finally
        {
            _cancellation = null;
            IsRunning = false;
        }
    }
}
