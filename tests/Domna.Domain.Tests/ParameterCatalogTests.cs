using Domna.Domain.Parameters;

namespace Domna.Domain.Tests;

public class ParameterCatalogTests
{
    [Fact]
    public void EveryParameterHasARange()
    {
        Assert.Equal(ParameterCatalog.Count, ParameterCatalog.All.Count);
    }

    [Theory]
    [MemberData(nameof(AllParameters))]
    public void RangeIsWellFormedAndHoldsItsDefault(Parameter parameter)
    {
        ParameterRange range = ParameterCatalog.RangeOf(parameter);

        Assert.True(range.Minimum < range.Maximum, $"{parameter}: minimum must be below maximum.");
        Assert.True(range.Contains(range.Default), $"{parameter}: default must sit inside the range.");
        Assert.InRange(range.Decimals, 0, 6);
    }

    [Fact]
    public void ParameterIndicesAreContiguousFromZero()
    {
        // The regression indexes its parameter vector by these values; a gap or a
        // repeat would silently shift every coefficient onto the wrong monomial.
        int[] indices = [.. ParameterCatalog.All.Select(p => (int)p).Order()];

        Assert.Equal(Enumerable.Range(0, ParameterCatalog.Count), indices);
    }

    [Fact]
    public void Clamp_PullsValuesBackToTheNearestBound()
    {
        ParameterRange range = ParameterCatalog.RangeOf(Parameter.BurdenLevel);

        Assert.Equal(range.Minimum, range.Clamp(range.Minimum - 10.0));
        Assert.Equal(range.Maximum, range.Clamp(range.Maximum + 10.0));
        Assert.Equal(range.Default, range.Clamp(range.Default));
    }

    public static TheoryData<Parameter> AllParameters()
    {
        TheoryData<Parameter> data = new();
        foreach (Parameter parameter in ParameterCatalog.All)
        {
            data.Add(parameter);
        }

        return data;
    }
}
