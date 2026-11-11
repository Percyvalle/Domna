using Domna.Domain.Models;

namespace Domna.Domain.Optimization;

/// <summary>How the optimizer constrains movements in the three charge-station parameters.</summary>
public abstract record ChargeStationMode
{
    private ChargeStationMode()
    {
    }

    /// <summary>
    /// The three station values are free continuous variables. Their sum is preserved
    /// across the search, and each remains within its fitted bounds.
    /// </summary>
    public sealed record Continuous : ChargeStationMode;

    /// <summary>
    /// The station values are derived from integer portion counts over the charge zones.
    /// The optimizer moves portions between zones rather than adjusting share values directly,
    /// so every candidate it evaluates corresponds to a physically realizable distribution.
    /// The ore and coke totals are conserved from <paramref name="Program"/>; stations 1–2
    /// are pinned. All three free zones move jointly.
    /// </summary>
    public sealed record Matrix(
        ChargeProgram Program,
        MatrixOptimizerMode MatrixMode = MatrixOptimizerMode.Aggregate) : ChargeStationMode;
}
