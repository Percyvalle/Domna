using Domna.Domain.Models;
using Domna.Domain.Parameters;

namespace Domna.Domain.Optimization;

/// <summary>
/// The inverse problem: which process parameters reproduce a wanted profile.
/// </summary>
/// <remarks>
/// Only <see cref="FreeParameters"/> may move. Everything else stays pinned at its
/// value in <see cref="Start"/>, because in the shop most of the eleven are not the
/// operator's to change on the day.
/// The sum of the three charge-station values is conserved from <see cref="Start"/>.
/// A station may move only if another free station can compensate within the
/// fitted bounds. If the free stations' starting sum cannot fit within those
/// bounds, their values remain pinned while other free parameters may move.
/// </remarks>
public sealed class OptimizationRequest
{
    /// <summary>The parameter set the search starts from and pins the fixed members to.</summary>
    public required ParameterSet Start { get; init; }

    /// <summary>The profile the search aims at — normally the baseline calculation.</summary>
    public required TemperatureProfile Target { get; init; }

    /// <summary>The parameters the optimizer is allowed to move, in any order.</summary>
    public required IReadOnlyList<Parameter> FreeParameters { get; init; }

    /// <summary>Algorithm tuning knobs applied to every independent search round.</summary>
    public SearchSettings Settings { get; init; } = new();
}
