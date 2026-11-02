using Domna.Domain.Interfaces;
using Domna.Domain.Models;

namespace Domna.Calculation;

/// <summary>
/// How far a parameter set lands from a wanted profile.
/// </summary>
/// <remarks>
/// The objective is written on the two ratios the regression fits directly, not on
/// the three temperatures. That is deliberate: the ratios describe the shape of the
/// gas flow, which is what the charge program steers and what the fit is good at,
/// while the absolute level follows the heat balance. Both terms are relative, so
/// neither ratio dominates the sum through its scale alone.
/// </remarks>
internal sealed class RatioObjective
{
    private readonly ITemperatureProfileCalculator _calculator;
    private readonly ProfileRatios _target;

    public RatioObjective(ITemperatureProfileCalculator calculator, TemperatureProfile target)
    {
        _calculator = calculator;
        _target = ProfileRatios.Of(target);
    }

    /// <summary>Objective value at <paramref name="parameters"/>; zero is an exact hit.</summary>
    /// <returns><see cref="double.MaxValue"/> where the polynomial leaves the reals,
    /// so such a point loses every comparison instead of poisoning the search.</returns>
    public double At(ParameterSet parameters)
    {
        TemperatureProfile profile = _calculator.Calculate(parameters);
        if (!profile.IsFinite)
        {
            return double.MaxValue;
        }

        ProfileRatios ratios = ProfileRatios.Of(profile);
        double residual =
            Squared(ratios.CenterToOreRidge, _target.CenterToOreRidge)
            + Squared(ratios.PeripheryToCenter, _target.PeripheryToCenter);

        return double.IsFinite(residual) ? residual : double.MaxValue;
    }

    private static double Squared(double actual, double target)
    {
        // A zero target means the profile it came from was already degenerate;
        // fall back to the absolute miss rather than dividing by it.
        double miss = target == 0.0 ? actual - target : (actual - target) / target;
        return miss * miss;
    }
}
