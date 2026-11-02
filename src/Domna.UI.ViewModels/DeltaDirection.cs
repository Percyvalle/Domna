namespace Domna.UI.ViewModels;

/// <summary>
/// Which way a value moved against the baseline calculation.
/// </summary>
/// <remarks>
/// The view colours the delta from this, rather than from the sign of a number it
/// would have to re-derive: the view models decide what a delta means, the view
/// only decides what it looks like.
/// </remarks>
public enum DeltaDirection
{
    /// <summary>There is no baseline, or no result to compare against it.</summary>
    Unavailable = 0,

    /// <summary>The difference is smaller than the shown precision.</summary>
    Unchanged = 1,

    /// <summary>Above the baseline.</summary>
    Up = 2,

    /// <summary>Below the baseline.</summary>
    Down = 3,
}
