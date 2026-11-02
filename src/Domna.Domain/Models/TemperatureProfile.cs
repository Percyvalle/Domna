namespace Domna.Domain.Models;

/// <summary>
/// The radial gas-temperature profile across the furnace top, in degrees Celsius.
/// </summary>
/// <remarks>
/// The three temperatures are the state; every ratio is derived from them, so a
/// profile cannot hold a temperature and a ratio that disagree.
/// </remarks>
/// <param name="Center">Gas temperature on the axis.</param>
/// <param name="OreRidge">Gas temperature at the ore ridge.</param>
/// <param name="Periphery">Gas temperature at the periphery.</param>
public readonly record struct TemperatureProfile(double Center, double OreRidge, double Periphery)
{
    /// <summary>
    /// Whether all three temperatures are real numbers.
    /// </summary>
    /// <remarks>
    /// A cubic polynomial evaluated far enough outside the fitted region overflows
    /// to infinity or NaN. That is not a temperature and must never reach the
    /// operator as one, so the screen shows the previous result and says so.
    /// </remarks>
    public bool IsFinite => double.IsFinite(Center) && double.IsFinite(OreRidge) && double.IsFinite(Periphery);

    /// <summary>The temperature at one position.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not a known position.</exception>
    public double At(TemperaturePosition position) => position switch
    {
        TemperaturePosition.Center => Center,
        TemperaturePosition.OreRidge => OreRidge,
        TemperaturePosition.Periphery => Periphery,
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, "Unknown measuring position."),
    };

    /// <summary>
    /// The ratio of two positions' temperatures — the number the operator actually
    /// reads, because it describes the shape of the gas flow rather than its level.
    /// </summary>
    /// <returns>Zero when the denominator is zero; a profile that degenerate is
    /// already outside the fitted region and must not propagate an infinity.</returns>
    public double Ratio(TemperaturePosition numerator, TemperaturePosition denominator)
    {
        double divisor = At(denominator);
        return divisor == 0.0 ? 0.0 : At(numerator) / divisor;
    }
}
