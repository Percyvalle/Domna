using Domna.Domain.Models;
using Domna.Domain.Parameters;

namespace Domna.Domain.Tests;

public class ParameterSetTests
{
    [Fact]
    public void Defaults_AreInsideTheFittedRegion()
    {
        Assert.Empty(ParameterSet.Defaults.OutOfRange());
    }

    [Fact]
    public void With_ReplacesOneValueAndLeavesTheOriginalUntouched()
    {
        ParameterSet original = ParameterSet.Defaults;
        double before = original[Parameter.BurdenLevel];

        ParameterSet changed = original.With(Parameter.BurdenLevel, 2.5);

        Assert.Equal(2.5, changed[Parameter.BurdenLevel]);
        Assert.Equal(before, original[Parameter.BurdenLevel]);
    }

    [Fact]
    public void With_LeavesEveryOtherParameterAlone()
    {
        ParameterSet changed = ParameterSet.Defaults.With(Parameter.BurdenLevel, 2.5);

        foreach (Parameter parameter in ParameterCatalog.All.Where(p => p != Parameter.BurdenLevel))
        {
            Assert.Equal(ParameterSet.Defaults[parameter], changed[parameter]);
        }
    }

    [Fact]
    public void From_RejectsAnIncompleteAssignment()
    {
        Dictionary<Parameter, double> incomplete = ParameterCatalog.All
            .Where(p => p != Parameter.TopGasPressure)
            .ToDictionary(p => p, p => ParameterCatalog.RangeOf(p).Default);

        Assert.Throws<ArgumentException>(() => ParameterSet.From(incomplete));
    }

    [Fact]
    public void OutOfRange_ReportsExactlyTheParametersOutsideTheirBounds()
    {
        ParameterRange range = ParameterCatalog.RangeOf(Parameter.BurdenLevel);
        ParameterSet set = ParameterSet.Defaults.With(Parameter.BurdenLevel, range.Maximum + 1.0);

        Assert.Equal(new[] { Parameter.BurdenLevel }, set.OutOfRange());
    }

    [Fact]
    public void ToVector_IsOrderedByTheParameterIndex()
    {
        IReadOnlyList<double> vector = ParameterSet.Defaults.ToVector();

        Assert.Equal(ParameterCatalog.Count, vector.Count);
        foreach (Parameter parameter in ParameterCatalog.All)
        {
            Assert.Equal(ParameterSet.Defaults[parameter], vector[(int)parameter]);
        }
    }
}
