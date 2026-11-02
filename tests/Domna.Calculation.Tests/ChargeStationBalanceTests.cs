using Domna.Domain.Interfaces;
using Domna.Domain.Models;
using Domna.Domain.Optimization;
using Domna.Domain.Parameters;

namespace Domna.Calculation.Tests;

public class ChargeStationBalanceTests
{
    private static readonly Parameter[] Stations =
    [
        Parameter.ChargeStations9To11,
        Parameter.ChargeStations6To8,
        Parameter.ChargeStations3To5,
    ];

    private readonly TemperatureProfileCalculator _calculator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Optimize_EveryCandidatePreservesSumAndFixedZones(int mask)
    {
        ParameterSet start = ParameterSet.Defaults
            .With(Stations[0], 78.3)
            .With(Stations[1], 52.1)
            .With(Stations[2], 40.7);
        Parameter[] free = [Parameter.BurdenLevel, .. FreeStations(mask)];
        BalanceCheckingCalculator checking = new(start, free);
        DifferentialEvolutionOptimizer optimizer = new(checking);
        OptimizationRequest request = new()
        {
            Start = start,
            Target = _calculator.Calculate(start.With(Stations[0], 90.0)),
            FreeParameters = free,
        };

        OptimizationOutcome outcome = optimizer.Optimize(request, null, CancellationToken.None);

        checking.Calculate(outcome.Parameters);
        Assert.True(checking.Calls > 100);
        Assert.False(outcome.Cancelled);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Optimize_ReachesABalancedRedistribution(int mask)
    {
        Parameter[] free = FreeStations(mask);
        ParameterSet start = ParameterSet.Defaults;
        ParameterSet target = start.With(free[0], start[free[0]] - 0.5)
            .With(free[1], start[free[1]] + 0.5);
        OptimizationRequest request = new()
        {
            Start = start,
            Target = _calculator.Calculate(target),
            FreeParameters = free,
        };
        BalanceCheckingCalculator checking = new(start, free);
        DifferentialEvolutionOptimizer optimizer = new(checking);

        OptimizationOutcome outcome = optimizer.Optimize(request, null, CancellationToken.None);

        checking.Calculate(outcome.Parameters);
        Assert.True(outcome.Residual < 1e-12, $"residual {outcome.Residual:e3} is too large");
        Assert.True(Math.Abs(outcome.Parameters[free[0]] - start[free[0]]) > 0.01);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Optimize_PreservesBalanceAtStationBounds(int corner)
    {
        ParameterSet start = ParameterSet.Defaults;
        for (int i = 0; i < Stations.Length; i++)
        {
            ParameterRange range = ParameterCatalog.RangeOf(Stations[i]);
            start = start.With(Stations[i], (corner & (1 << i)) == 0 ? range.Minimum : range.Maximum);
        }

        BalanceCheckingCalculator checking = new(start, Stations);
        DifferentialEvolutionOptimizer optimizer = new(checking);
        OptimizationRequest request = new()
        {
            Start = start,
            Target = new TemperatureProfile(1000.0, 20.0, 900.0),
            FreeParameters = Stations,
        };

        OptimizationOutcome outcome = optimizer.Optimize(request, null, CancellationToken.None);

        checking.Calculate(outcome.Parameters);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Optimize_CancelledSearchStillPreservesBalance(bool afterRound)
    {
        using CancellationTokenSource cancellation = new();
        if (!afterRound)
        {
            cancellation.Cancel();
        }

        BalanceCheckingCalculator checking = new(ParameterSet.Defaults, Stations);
        DifferentialEvolutionOptimizer optimizer = new(checking);
        OptimizationRequest request = new()
        {
            Start = ParameterSet.Defaults,
            Target = new TemperatureProfile(1000.0, 20.0, 900.0),
            FreeParameters = Stations,
        };

        OptimizationOutcome outcome = optimizer.Optimize(request, _ => cancellation.Cancel(), cancellation.Token);

        checking.Calculate(outcome.Parameters);
        Assert.True(outcome.Cancelled);
    }

    [Fact]
    public void Optimize_StationSearchIsDeterministicWithRepeatedAndReorderedFreeParameters()
    {
        DifferentialEvolutionOptimizer optimizer = new(_calculator);
        OptimizationRequest request = new()
        {
            Start = ParameterSet.Defaults,
            Target = _calculator.Calculate(ParameterSet.Defaults.With(Stations[0], 73.0).With(Stations[1], 52.0)),
            FreeParameters = [Stations[2], Stations[0], Stations[1], Stations[0]],
        };

        OptimizationOutcome first = optimizer.Optimize(request, null, CancellationToken.None);
        OptimizationOutcome second = optimizer.Optimize(request, null, CancellationToken.None);

        Assert.Equal(first.Parameters.ToVector(), second.Parameters.ToVector());
        Assert.Equal(Total(request.Start), Total(first.Parameters), 10);
        Assert.True(first.Residual < 1e-12);
    }

    [Fact]
    public void Optimize_FeasibleTotalFromOutOfRangeStationsUsesOnlyBoundedCandidates()
    {
        ParameterSet start = ParameterSet.Defaults
            .With(Stations[0], 110.0)
            .With(Stations[1], 30.0)
            .With(Stations[2], 25.0);
        BalanceCheckingCalculator checking = new(start, Stations);
        DifferentialEvolutionOptimizer optimizer = new(checking);
        OptimizationRequest request = new()
        {
            Start = start,
            Target = _calculator.Calculate(ParameterSet.Defaults),
            FreeParameters = Stations,
        };

        OptimizationOutcome outcome = optimizer.Optimize(request, null, CancellationToken.None);

        checking.Calculate(outcome.Parameters);
        Assert.True(outcome.Residual < 1e-12);
    }

    [Fact]
    public void Optimize_InfeasibleStationTotalStaysPinnedWhileOtherParametersMove()
    {
        ParameterSet start = ParameterSet.Defaults.With(Stations[0], 10.0);
        DifferentialEvolutionOptimizer optimizer = new(_calculator);
        OptimizationRequest request = new()
        {
            Start = start,
            Target = _calculator.Calculate(start.With(Parameter.BurdenLevel, 2.4)),
            FreeParameters = [.. Stations, Parameter.BurdenLevel],
        };

        OptimizationOutcome outcome = optimizer.Optimize(request, null, CancellationToken.None);

        foreach (Parameter parameter in Stations)
        {
            Assert.Equal(start[parameter], outcome.Parameters[parameter]);
        }

        Assert.True(outcome.Residual < 1e-12);
    }

    private static Parameter[] FreeStations(int mask) =>
        [.. Stations.Where((_, i) => (mask & (1 << i)) != 0)];

    private static double Total(ParameterSet parameters) => Stations.Sum(parameter => parameters[parameter]);

    private sealed class BalanceCheckingCalculator(ParameterSet start, Parameter[] free) : ITemperatureProfileCalculator
    {
        private readonly TemperatureProfileCalculator _calculator = new();

        public int Calls { get; private set; }

        public TemperatureProfile Calculate(ParameterSet parameters)
        {
            Calls++;
            Assert.Equal(Total(start), Total(parameters), 10);
            Assert.Empty(parameters.OutOfRange());
            foreach (Parameter parameter in ParameterCatalog.All.Where(parameter => !free.Contains(parameter)))
            {
                Assert.Equal(start[parameter], parameters[parameter]);
            }

            return _calculator.Calculate(parameters);
        }
    }
}
