namespace Domna.Domain.Optimization;

/// <summary>How the optimizer redistributes portions in the charge matrix.</summary>
public enum MatrixOptimizerMode
{
    /// <summary>
    /// All ore portions are pooled across drops before redistribution, and likewise
    /// for coke. The optimizer moves aggregate zone totals freely, so material can
    /// effectively shift between drops of the same type. 2(N-1) search dimensions for N free zones.
    /// </summary>
    Aggregate,

    /// <summary>
    /// Each drop's zone total is fixed; the optimizer only moves portions horizontally
    /// between zones within the same drop. No material transfers between drops.
    /// (N-1) × D search dimensions for N free zones and D drops.
    /// </summary>
    PerDrop,
}
