using Domna.Domain.Models;
using Domna.Domain.Optimization;
using Domna.Domain.Parameters;

namespace Domna.Calculation.Tests;

public class DifferentialEvolutionOptimizerTests
{
    private readonly TemperatureProfileCalculator _calculator = new();
    private readonly DifferentialEvolutionOptimizer _optimizer;

    public DifferentialEvolutionOptimizerTests() => _optimizer = new DifferentialEvolutionOptimizer(_calculator);

    [Fact]
    public void Optimize_IsDeterministic()
    {
        OptimizationRequest request = RequestReaching(Parameter.BurdenLevel, 2.4);

        OptimizationOutcome first = _optimizer.Optimize(request, null, CancellationToken.None);
        OptimizationOutcome second = _optimizer.Optimize(request, null, CancellationToken.None);

        Assert.Equal(first.Residual, second.Residual);
        Assert.Equal(first.Parameters.ToVector(), second.Parameters.ToVector());
    }

    [Fact]
    public void Optimize_RecoversAParameterItWasAllowedToMove()
    {
        OptimizationRequest request = RequestReaching(Parameter.BurdenLevel, 2.4);

        OptimizationOutcome outcome = _optimizer.Optimize(request, null, CancellationToken.None);

        // The two fitted ratios are what the search matches, so it is the ratios that
        // must land, not the parameter — but with one free parameter this one does too.
        Assert.True(outcome.Residual < 1e-6, $"residual {outcome.Residual} is too large");
        Assert.Equal(2.4, outcome.Parameters[Parameter.BurdenLevel], 2);
    }

    [Fact]
    public void Optimize_LeavesFixedParametersWhereTheyWere()
    {
        OptimizationRequest request = RequestReaching(Parameter.BurdenLevel, 2.4);

        OptimizationOutcome outcome = _optimizer.Optimize(request, null, CancellationToken.None);

        foreach (Parameter parameter in ParameterCatalog.All)
        {
            if (parameter != Parameter.BurdenLevel)
            {
                Assert.Equal(ParameterSet.Defaults[parameter], outcome.Parameters[parameter]);
            }
        }
    }

    [Fact]
    public void Optimize_NeverLeavesTheFittedRegion()
    {
        // An unreachable target: the search has to report its best point, and that
        // point still has to be one the regression was fitted for.
        OptimizationRequest request = new()
        {
            Start = ParameterSet.Defaults,
            Target = new TemperatureProfile(1000.0, 20.0, 900.0),
            FreeParameters = [.. ParameterCatalog.All],
        };

        OptimizationOutcome outcome = _optimizer.Optimize(request, null, CancellationToken.None);

        Assert.Empty(outcome.Parameters.OutOfRange());
    }

    [Fact]
    public void Optimize_WithNothingFree_ReturnsTheStartPoint()
    {
        OptimizationRequest request = new()
        {
            Start = ParameterSet.Defaults,
            Target = _calculator.Calculate(ParameterSet.Defaults),
            FreeParameters = [],
        };

        OptimizationOutcome outcome = _optimizer.Optimize(request, null, CancellationToken.None);

        Assert.Equal(ParameterSet.Defaults.ToVector(), outcome.Parameters.ToVector());
        Assert.Equal(0, outcome.Rounds);
        Assert.False(outcome.Cancelled);
    }

    [Fact]
    public void Optimize_ReportsOneProgressPerRound()
    {
        List<OptimizationProgress> reports = [];

        OptimizationOutcome outcome = _optimizer.Optimize(
            RequestReaching(Parameter.BurdenLevel, 2.4),
            reports.Add,
            CancellationToken.None);

        Assert.Equal(outcome.Rounds, reports.Count);
        Assert.Equal(Enumerable.Range(1, reports.Count), reports.Select(r => r.Round));

        // The best-so-far residual can only improve.
        Assert.Equal(reports.Select(r => r.Residual).OrderDescending(), reports.Select(r => r.Residual));
    }

    [Fact]
    public void Optimize_WhenCancelled_ReportsItAndStillReturnsAValidPoint()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OptimizationOutcome outcome = _optimizer.Optimize(
            RequestReaching(Parameter.BurdenLevel, 2.4),
            null,
            cancellation.Token);

        Assert.True(outcome.Cancelled);
        Assert.Empty(outcome.Parameters.OutOfRange());
    }

    [Fact]
    public void Optimize_NelderMeadPolishTightensResidualBelowDeApproximation()
    {
        // For a 1-free-parameter problem the polish step is effectively bisection and
        // should converge to well below the DE tolerance of 1e-6.
        OptimizationRequest request = RequestReaching(Parameter.BurdenLevel, 2.4);

        OptimizationOutcome outcome = _optimizer.Optimize(request, null, CancellationToken.None);

        Assert.True(outcome.Residual < 1e-12, $"residual {outcome.Residual:e3} exceeds NM polish threshold");
    }

    [Fact]
    public void Optimize_RejectsANullRequest()
    {
        Assert.Throws<ArgumentNullException>(() => _optimizer.Optimize(null!, null, CancellationToken.None));
    }

    /// <summary>A request whose target is the profile of one moved parameter.</summary>
    private OptimizationRequest RequestReaching(Parameter parameter, double value) => new()
    {
        Start = ParameterSet.Defaults,
        Target = _calculator.Calculate(ParameterSet.Defaults.With(parameter, value)),
        FreeParameters = [parameter],
    };
}
