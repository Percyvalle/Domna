using System.Globalization;
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
            _optimizer,
            new RecordingExportTarget(),
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 2, 12, 34, 56, TimeSpan.Zero)));
        _screen.SaveSnapshot();
        Input(Parameter.ChargeStations9To11).IsFree = true;
        Input(Parameter.ChargeStations6To8).IsFree = true;
        _optimizer.Answer = ParameterSet.Defaults
            .With(Parameter.ChargeStations9To11, 73.0)
            .With(Parameter.ChargeStations6To8, 52.0);
    }

    [Fact]
    public async Task CompletedOptimizationShowsChangedValuesAndLeavesFixedParametersBlank()
    {
        await RunOptimizationAsync();

        Assert.Equal(73.0, Input(Parameter.ChargeStations9To11).Value);
        Assert.Equal("75,0 → 73,0", Input(Parameter.ChargeStations9To11).OptimizerChange);
        Assert.Equal("50,00 → 52,00", Input(Parameter.ChargeStations6To8).OptimizerChange);
        Assert.All(
            _screen.Inputs.Where(input => !input.IsFree),
            input => Assert.Empty(input.OptimizerChange));
    }

    [Fact]
    public async Task ComparisonUsesTheSearchStartRatherThanUncommittedEdits()
    {
        Input(Parameter.ChargeStations9To11).Value = 80.0;

        await RunOptimizationAsync();

        Assert.Equal(75.0, _optimizer.LastRequest!.Start[Parameter.ChargeStations9To11]);
        Assert.Equal("75,0 → 73,0", Input(Parameter.ChargeStations9To11).OptimizerChange);
    }

    [Fact]
    public async Task RepeatedOptimizationShowsTheLatestTransition()
    {
        await RunOptimizationAsync();
        _optimizer.Answer = _optimizer.Answer!
            .With(Parameter.ChargeStations9To11, 74.0)
            .With(Parameter.ChargeStations6To8, 51.0);

        await RunOptimizationAsync();

        Assert.Equal("73,0 → 74,0", Input(Parameter.ChargeStations9To11).OptimizerChange);
        Assert.Equal("52,00 → 51,00", Input(Parameter.ChargeStations6To8).OptimizerChange);
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
