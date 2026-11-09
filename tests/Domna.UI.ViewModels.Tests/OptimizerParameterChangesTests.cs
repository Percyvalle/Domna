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
        _screen.SaveSnapshot();
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
