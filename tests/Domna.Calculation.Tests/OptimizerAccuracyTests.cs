using System.Globalization;
using Domna.Domain.Models;
using Domna.Domain.Optimization;
using Domna.Domain.Parameters;

namespace Domna.Calculation.Tests;

public class OptimizerAccuracyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Optimize_EscapesTheKnownLocalMinimum(bool reverse)
    {
        ParameterSet start = ParameterSet.Defaults
            .With(Parameter.BurdenLevel, 2.7337802041246464)
            .With(Parameter.OreLoadRate, 3.748181146657179)
            .With(Parameter.CokeSize, 56.621125490554206)
            .With(Parameter.SinterSize, 9.976836435174027)
            .With(Parameter.PelletsSize, 13.811022591409749)
            .With(Parameter.PelletsRatio, 0.29928348407861005)
            .With(Parameter.HotBlastPressure, 2.724214493657562)
            .With(Parameter.TopGasPressure, 1.429226338904922);
        ParameterSet target = start
            .With(Parameter.BurdenLevel, 1.6469117578803152)
            .With(Parameter.TopGasPressure, 1.4582481932585352);
        Parameter[] free = reverse
            ? [Parameter.TopGasPressure, Parameter.BurdenLevel]
            : [Parameter.BurdenLevel, Parameter.TopGasPressure];

        AssertReaches(start, target, free, 1e-9);
    }

    [Theory]
    [MemberData(nameof(ReachableTargets))]
    public void Optimize_ReachesKnownTargetsFromDifferentStarts(
        ParameterSet start,
        ParameterSet target,
        Parameter[] free) => AssertReaches(start, target, free, 1e-6);

    [Fact]
    public void Optimize_FindsTheBetterBasinForADistantTarget()
    {
        TemperatureProfileCalculator calculator = new();
        TemperatureProfile target = new(300.0, 300.0 / 3.9311067277011955, 300.0 * 2.1795114540120175);
        ParameterSet knownCandidate = ParameterSet.Defaults
            .With(Parameter.BurdenLevel, 3.10)
            .With(Parameter.OreLoadRate, 4.57)
            .With(Parameter.CokeSize, 50.0)
            .With(Parameter.SinterSize, 10.886947239502378)
            .With(Parameter.PelletsSize, 12.68)
            .With(Parameter.PelletsRatio, 0.505)
            .With(Parameter.ChargeStations9To11, 67.35)
            .With(Parameter.ChargeStations6To8, 58.82)
            .With(Parameter.ChargeStations3To5, 38.83)
            .With(Parameter.HotBlastPressure, 2.45)
            .With(Parameter.TopGasPressure, 1.37);
        OptimizationRequest request = new()
        {
            Start = ParameterSet.Defaults,
            Target = target,
            FreeParameters = [.. ParameterCatalog.All],
        };

        OptimizationOutcome outcome = new DifferentialEvolutionOptimizer(calculator).Optimize(request, null, CancellationToken.None);

        double feasibleResidual = Residual(calculator.Calculate(knownCandidate), target);
        Assert.True(outcome.Residual <= feasibleResidual + 1e-8);
        Assert.Empty(outcome.Parameters.OutOfRange());
    }

    public static IEnumerable<object[]> ReachableTargets()
    {
        Parameter[] parameters = [.. ParameterCatalog.All.Where(p => p is not (
            Parameter.ChargeStations9To11 or Parameter.ChargeStations6To8 or Parameter.ChargeStations3To5))];
        Random sequence = new(717);
        TemperatureProfileCalculator calculator = new();

        for (int a = 0; a < parameters.Length; a++)
        {
            for (int b = a + 1; b < parameters.Length; b++)
            {
                for (int sample = 0; sample < 20; sample++)
                {
                    ParameterSet start = Sample(ParameterSet.Defaults, parameters, sequence);
                    Parameter[] free = [parameters[a], parameters[b]];
                    ParameterSet target = Sample(start, free, sequence);
                    TemperatureProfile profile = calculator.Calculate(target);
                    if (profile.Center > 0.0 && profile.OreRidge > 0.0 && profile.Periphery > 0.0)
                    {
                        yield return [start, target, free];
                    }
                }
            }
        }
    }

    private static ParameterSet Sample(ParameterSet start, Parameter[] parameters, Random sequence)
    {
        foreach (Parameter parameter in parameters)
        {
            ParameterRange range = ParameterCatalog.RangeOf(parameter);
            start = start.With(parameter, range.Minimum + (sequence.NextDouble() * (range.Maximum - range.Minimum)));
        }

        return start;
    }

    private static double Residual(TemperatureProfile profile, TemperatureProfile target)
    {
        ProfileRatios actual = ProfileRatios.Of(profile);
        ProfileRatios wanted = ProfileRatios.Of(target);
        double center = (actual.CenterToOreRidge - wanted.CenterToOreRidge) / wanted.CenterToOreRidge;
        double periphery = (actual.PeripheryToCenter - wanted.PeripheryToCenter) / wanted.PeripheryToCenter;
        return (center * center) + (periphery * periphery);
    }

    private static void AssertReaches(ParameterSet start, ParameterSet target, Parameter[] free, double tolerance)
    {
        TemperatureProfileCalculator calculator = new();
        TemperatureProfile expected = calculator.Calculate(target);
        OptimizationRequest request = new() { Start = start, Target = expected, FreeParameters = free };

        OptimizationOutcome outcome = new DifferentialEvolutionOptimizer(calculator).Optimize(request, null, CancellationToken.None);

        ProfileRatios actual = ProfileRatios.Of(calculator.Calculate(outcome.Parameters));
        ProfileRatios wanted = ProfileRatios.Of(expected);
        Assert.True(outcome.Residual < tolerance * tolerance, string.Create(
            CultureInfo.InvariantCulture, $"Unreached feasible target: residual {outcome.Residual:R}"));
        Assert.InRange(Math.Abs((actual.CenterToOreRidge - wanted.CenterToOreRidge) / wanted.CenterToOreRidge), 0.0, tolerance);
        Assert.InRange(Math.Abs((actual.PeripheryToCenter - wanted.PeripheryToCenter) / wanted.PeripheryToCenter), 0.0, tolerance);
        Assert.Empty(outcome.Parameters.OutOfRange());
        Assert.False(outcome.Cancelled);
        foreach (Parameter parameter in ParameterCatalog.All.Except(free))
        {
            Assert.Equal(start[parameter], outcome.Parameters[parameter]);
        }
    }
}
