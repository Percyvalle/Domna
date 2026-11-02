using Domna.Domain.Interfaces;
using Domna.Domain.Models;
using Domna.Domain.Optimization;
using Domna.Domain.Parameters;

namespace Domna.Calculation.Tests;

public class OptimizerCancellationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void Optimize_CancellationAfterCompletedRoundIsReported(int round)
    {
        using CancellationTokenSource cancellation = new();
        TemperatureProfileCalculator calculator = new();
        OptimizationRequest request = Request(calculator);

        OptimizationOutcome outcome = new DifferentialEvolutionOptimizer(calculator).Optimize(
            request,
            progress =>
            {
                if (progress.Round == round)
                {
                    cancellation.Cancel();
                }
            },
            cancellation.Token);

        Assert.True(outcome.Cancelled);
        Assert.Equal(round, outcome.Rounds);
        Assert.Empty(outcome.Parameters.OutOfRange());
    }

    [Fact]
    public void Optimize_CancellationRetainsTheBestEvaluatedPoint()
    {
        using CancellationTokenSource cancellation = new();
        TemperatureProfileCalculator calculator = new();
        OptimizationRequest request = Request(calculator);
        TrackingCalculator tracking = new(request.Target, cancellation);

        OptimizationOutcome outcome = new DifferentialEvolutionOptimizer(tracking).Optimize(request, null, cancellation.Token);

        Assert.True(outcome.Cancelled);
        Assert.Equal(tracking.BestResidual, outcome.Residual);
        Assert.Equal(tracking.BestResidual, tracking.Residual(calculator.Calculate(outcome.Parameters)));
    }

    [Fact]
    public void Optimize_PreCancelledRequestWithNoFreeParametersIsReported()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        TemperatureProfileCalculator calculator = new();
        OptimizationRequest request = new()
        {
            Start = ParameterSet.Defaults,
            Target = calculator.Calculate(ParameterSet.Defaults),
            FreeParameters = [],
        };

        OptimizationOutcome outcome = new DifferentialEvolutionOptimizer(calculator).Optimize(request, null, cancellation.Token);

        Assert.True(outcome.Cancelled);
        Assert.Equal(0, outcome.Rounds);
        Assert.Same(request.Start, outcome.Parameters);
    }

    private static OptimizationRequest Request(TemperatureProfileCalculator calculator) => new()
    {
        Start = ParameterSet.Defaults,
        Target = calculator.Calculate(ParameterSet.Defaults.With(Parameter.BurdenLevel, 2.4)),
        FreeParameters = [.. ParameterCatalog.All],
    };

    private sealed class TrackingCalculator(TemperatureProfile target, CancellationTokenSource cancellation)
        : ITemperatureProfileCalculator
    {
        private readonly TemperatureProfileCalculator _calculator = new();
        private readonly ProfileRatios _target = ProfileRatios.Of(target);
        private int _calls;

        public double BestResidual { get; private set; } = double.MaxValue;

        public TemperatureProfile Calculate(ParameterSet parameters)
        {
            TemperatureProfile profile = _calculator.Calculate(parameters);
            BestResidual = Math.Min(BestResidual, Residual(profile));
            if (++_calls == 500)
            {
                cancellation.Cancel();
            }

            return profile;
        }

        public double Residual(TemperatureProfile profile)
        {
            ProfileRatios ratios = ProfileRatios.Of(profile);
            double center = (ratios.CenterToOreRidge - _target.CenterToOreRidge) / _target.CenterToOreRidge;
            double periphery = (ratios.PeripheryToCenter - _target.PeripheryToCenter) / _target.PeripheryToCenter;
            return (center * center) + (periphery * periphery);
        }
    }
}
