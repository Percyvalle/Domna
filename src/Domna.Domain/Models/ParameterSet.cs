using Domna.Domain.Parameters;

namespace Domna.Domain.Models;

/// <summary>
/// An immutable, complete assignment of all eleven process parameters — one
/// point in the model's input space.
/// </summary>
/// <remarks>
/// Complete by construction: there is no partially filled state, so the
/// calculator never has to ask whether a parameter was supplied. Mutation goes
/// through <see cref="With(Parameter, double)"/>, which returns a new set.
/// </remarks>
public sealed class ParameterSet
{
    private readonly double[] _values;

    private ParameterSet(double[] values) => _values = values;

    /// <summary>The parameter set every session starts from, per <see cref="ParameterCatalog"/>.</summary>
    public static ParameterSet Defaults { get; } = new(
        [.. ParameterCatalog.All.Select(p => ParameterCatalog.RangeOf(p).Default)]);

    /// <summary>The value of one parameter.</summary>
    public double this[Parameter parameter] => _values[(int)parameter];

    /// <summary>Builds a set from every parameter's value.</summary>
    /// <exception cref="ArgumentException">A parameter is missing from <paramref name="values"/>.</exception>
    public static ParameterSet From(IReadOnlyDictionary<Parameter, double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        double[] vector = new double[ParameterCatalog.Count];
        foreach (Parameter parameter in ParameterCatalog.All)
        {
            if (!values.TryGetValue(parameter, out double value))
            {
                throw new ArgumentException($"Missing value for parameter {parameter}.", nameof(values));
            }

            vector[(int)parameter] = value;
        }

        return new ParameterSet(vector);
    }

    /// <summary>This set with one parameter replaced.</summary>
    public ParameterSet With(Parameter parameter, double value)
    {
        double[] vector = (double[])_values.Clone();
        vector[(int)parameter] = value;
        return new ParameterSet(vector);
    }

    /// <summary>The parameters whose value falls outside the fitted region.</summary>
    public IEnumerable<Parameter> OutOfRange() =>
        ParameterCatalog.All.Where(p => !ParameterCatalog.RangeOf(p).Contains(this[p]));

    /// <summary>The values in parameter-vector order — the form the optimizer searches over.</summary>
    public IReadOnlyList<double> ToVector() => _values.AsReadOnly();
}
