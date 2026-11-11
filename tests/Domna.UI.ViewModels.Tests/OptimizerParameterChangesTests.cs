using System.Globalization;
using Domna.Calculation;
using Domna.Domain.Models;
using Domna.Domain.Parameters;

namespace Domna.UI.ViewModels.Tests;

public class OptimizerParameterChangesTests
{
    private readonly StubOptimizer _optimizer = new();
    private readonly MainViewModel _screen;

    public OptimizerParameterChangesTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        _screen = new MainViewModel(
            new StubCalculator(),
            new ChargeProgramCalculator(),
            _optimizer,
            new RecordingExportTarget(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 2, 12, 34, 56, TimeSpan.Zero)));
        _screen.Calculate();
        Input(Parameter.BurdenLevel).IsFree = true;
        Input(Parameter.CokeSize).IsFree = true;
        _optimizer.Answer = ParameterSet.Defaults
            .With(Parameter.BurdenLevel, 1.9)
            .With(Parameter.CokeSize, 54.0);
    }

    [Fact]
    public async Task CompletedOptimizationShowsChangedValuesAndLeavesFixedParametersBlank()
    {
        await RunOptimizationAsync();

        Assert.Equal(1.9, Input(Parameter.BurdenLevel).Value);
        Assert.Equal("1,857 → 1,900", Input(Parameter.BurdenLevel).OptimizerChange);
        Assert.Equal("53,88 → 54,00", Input(Parameter.CokeSize).OptimizerChange);
        Assert.All(
            _screen.Inputs.Where(input => !input.IsFree),
            input => Assert.Empty(input.OptimizerChange));
    }

    [Fact]
    public async Task ComparisonUsesTheSearchStartRatherThanUncommittedEdits()
    {
        Input(Parameter.BurdenLevel).Value = 2.0;

        await RunOptimizationAsync();

        Assert.Equal(1.857, _optimizer.LastRequest!.Start[Parameter.BurdenLevel]);
        Assert.Equal("1,857 → 1,900", Input(Parameter.BurdenLevel).OptimizerChange);
    }

    [Fact]
    public async Task RepeatedOptimizationShowsTheLatestTransition()
    {
        await RunOptimizationAsync();
        _optimizer.Answer = _optimizer.Answer!
            .With(Parameter.BurdenLevel, 2.0)
            .With(Parameter.CokeSize, 55.0);

        await RunOptimizationAsync();

        Assert.Equal("1,900 → 2,000", Input(Parameter.BurdenLevel).OptimizerChange);
        Assert.Equal("54,00 → 55,00", Input(Parameter.CokeSize).OptimizerChange);
    }

    [Theory]
    [InlineData("Reset")]
    [InlineData("Restore")]
    [InlineData("Load")]
    public async Task ReplacingInputsClearsTheOptimizationChanges(string action)
    {
        await RunOptimizationAsync();

        switch (action)
        {
            case "Reset":
                _screen.Reset();
                break;
            case "Restore":
                _screen.RestoreLastInput();
                break;
            case "Load":
                _screen.History[0].Load();
                break;
        }

        Assert.All(_screen.Inputs, input => Assert.Empty(input.OptimizerChange));
    }

    [Fact]
    public async Task MatrixOptimizationShowsZoneDiffInHistoryEntry()
    {
        // Program with material in all three zones (required by ChargeProgramCalculator).
        // Coke: stations 3=1, 6=1, 9=2 → zone 9–11 has 2 coke. Ore: 1 each across zones.
        ChargeProgramEditorViewModel editor = _screen.EditChargeProgram();
        editor.Rows[0].Portions[2] = "1"; // coke station 3
        editor.Rows[0].Portions[5] = "1"; // coke station 6
        editor.Rows[0].Portions[8] = "2"; // coke station 9
        editor.Rows[1].Portions[2] = "1"; // ore station 3
        editor.Rows[1].Portions[5] = "1"; // ore station 6
        editor.Rows[1].Portions[8] = "1"; // ore station 9
        Assert.True(editor.TryApply(), editor.Error);
        _screen.Calculate();

        // Optimizer moves 2 coke from zone 9–11 to zone 3–5; ore unchanged.
        // Zone 9–11: before=3 (2coke+1ore), after=1 (0coke+1ore) → "9–11: 3→1".
        // Zone 3–5: before=2 (1coke+1ore), after=4 (3coke+1ore) → "3–5: 2→4".
        int[] cokeRedistributed = new int[ChargeDrop.StationCount];
        cokeRedistributed[2] = 3; // station 3: total coke in zone 3–5 = 3
        cokeRedistributed[5] = 1; // station 6
        int[] oreUnchanged = new int[ChargeDrop.StationCount];
        oreUnchanged[2] = 1; // station 3
        oreUnchanged[5] = 1; // station 6
        oreUnchanged[8] = 1; // station 9
        _optimizer.AnswerProgram = new ChargeProgram([
            new ChargeDrop(ChargeMaterial.Coke, cokeRedistributed),
            new ChargeDrop(ChargeMaterial.Ore, oreUnchanged),
        ]);

        _screen.Inputs[(int)Parameter.ChargeStations9To11].IsFree = true;
        await RunOptimizationAsync();

        HistoryEntryViewModel latest = _screen.History[^1];
        Assert.True(latest.HasMatrixDiff);
        Assert.Contains("9–11: 3→1", latest.MatrixDiff, StringComparison.Ordinal);
        Assert.Contains("3–5: 2→4", latest.MatrixDiff, StringComparison.Ordinal);
    }

    private ParameterInputViewModel Input(Parameter parameter) => _screen.Inputs[(int)parameter];

    private async Task RunOptimizationAsync()
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(OptimizationViewModel.IsRunning) && !_screen.Optimization.IsRunning)
            {
                completion.TrySetResult();
            }
        }

        _screen.Optimization.PropertyChanged += OnChanged;
        try
        {
            _screen.Optimization.Run();
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            _screen.Optimization.PropertyChanged -= OnChanged;
        }
    }
}
