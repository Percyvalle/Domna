namespace Domna.Domain.Parameters;

/// <summary>
/// The admissible interval and display precision of one process parameter.
/// </summary>
/// <param name="Minimum">Lower bound of the fitted region.</param>
/// <param name="Maximum">Upper bound of the fitted region.</param>
/// <param name="Default">Value the parameter starts at and resets to.</param>
/// <param name="Decimals">Decimal places to show and round to in the UI.</param>
public readonly record struct ParameterRange(double Minimum, double Maximum, double Default, int Decimals)
{
    /// <summary>Whether <paramref name="value"/> lies inside the fitted region, bounds included.</summary>
    public bool Contains(double value) => value >= Minimum && value <= Maximum;

    /// <summary>Nearest value inside the fitted region.</summary>
    public double Clamp(double value) => Math.Clamp(value, Minimum, Maximum);
}
