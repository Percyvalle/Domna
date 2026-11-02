using Domna.Domain.Models;

namespace Domna.Domain.Interfaces;

/// <summary>
/// Turns a complete set of process parameters into a top gas-temperature profile.
/// </summary>
/// <remarks>
/// Implementations are pure: same input, same output, no I/O, no clock, no state.
/// The UI relies on that to recalculate on every keystroke.
/// </remarks>
public interface ITemperatureProfileCalculator
{
    /// <summary>Computes the profile for <paramref name="parameters"/>.</summary>
    TemperatureProfile Calculate(ParameterSet parameters);
}
