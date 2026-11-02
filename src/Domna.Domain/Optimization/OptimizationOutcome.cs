using Domna.Domain.Models;

namespace Domna.Domain.Optimization;

/// <summary>
/// What a search returned: the best parameter set found and how close it got.
/// </summary>
/// <remarks>
/// <paramref name="Residual"/> is the objective value, not a promise: the optimizer
/// always returns its best point, and only the residual distinguishes a hit from
/// "this is as close as the fitted region allows".
/// </remarks>
/// <param name="Parameters">The best parameter set found, inside the fitted region.</param>
/// <param name="Residual">Objective value at <paramref name="Parameters"/>; zero is an exact hit.</param>
/// <param name="Rounds">Number of search rounds actually run.</param>
/// <param name="Cancelled">True when cancellation was requested before the outcome was returned.</param>
public readonly record struct OptimizationOutcome(
    ParameterSet Parameters,
    double Residual,
    int Rounds,
    bool Cancelled);
