using Domna.Domain.Models;
using Domna.Domain.Parameters;

namespace Domna.Calculation.Tests;

public class TemperatureProfileCalculatorTests
{
    private readonly TemperatureProfileCalculator _calculator = new();

    [Fact]
    public void DefaultParameters_ReproduceTheReferenceProfile()
    {
        // Reference values computed from the fitted expansions at the catalogue
        // defaults, independently of this implementation. They pin the port: any
        // drift in a coefficient, a monomial or a term order breaks this test and
        // nothing else would.
        TemperatureProfile profile = _calculator.Calculate(ParameterSet.Defaults);

        Assert.Equal(272.59362591618179, profile.Center, 1e-9);
        Assert.Equal(139.41992678663084, profile.OreRidge, 1e-9);
        Assert.Equal(170.40870586815851, profile.Periphery, 1e-9);
    }

    [Fact]
    public void DefaultParameters_ProducePhysicallyOrderedTemperatures()
    {
        TemperatureProfile profile = _calculator.Calculate(ParameterSet.Defaults);

        Assert.True(profile.Center > 0.0);
        Assert.True(profile.OreRidge > 0.0);
        Assert.True(profile.Periphery > 0.0);

        // The ore ridge is where the charge piles up, so it is the coldest of the three.
        Assert.True(profile.OreRidge < profile.Periphery);
        Assert.True(profile.Periphery < profile.Center);
    }

    [Fact]
    public void FittedRegionBoundaries_ProducePositiveTemperatures()
    {
        foreach (ParameterSet parameters in BoundaryParameterSets())
        {
            TemperatureProfile profile = _calculator.Calculate(parameters);

            Assert.True(profile.Center > 0.0, "centre temperature must stay positive across the fitted region");
            Assert.True(profile.OreRidge > 0.0, "ore-ridge temperature must stay positive across the fitted region");
            Assert.True(profile.Periphery > 0.0, "periphery temperature must stay positive across the fitted region");
        }
    }

    [Fact]
    public void Calculation_IsPure()
    {
        ParameterSet parameters = ParameterSet.Defaults;

        TemperatureProfile first = _calculator.Calculate(parameters);
        TemperatureProfile second = _calculator.Calculate(parameters);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ChangingOneParameter_MovesTheProfile()
    {
        ParameterSet hotter = ParameterSet.Defaults.With(Parameter.BurdenLevel, 2.5);

        TemperatureProfile baseline = _calculator.Calculate(ParameterSet.Defaults);
        TemperatureProfile changed = _calculator.Calculate(hotter);

        Assert.NotEqual(baseline.Center, changed.Center);
    }

    [Fact]
    public void Calculate_RejectsANullParameterSet()
    {
        Assert.Throws<ArgumentNullException>(() => _calculator.Calculate(null!));
    }

    private static IEnumerable<ParameterSet> BoundaryParameterSets()
    {
        yield return ParameterSet.From(
            ParameterCatalog.All.ToDictionary(p => p, p => ParameterCatalog.RangeOf(p).Minimum));
        yield return ParameterSet.From(
            ParameterCatalog.All.ToDictionary(p => p, p => ParameterCatalog.RangeOf(p).Maximum));
    }
}
