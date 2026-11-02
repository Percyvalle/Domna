using Domna.Domain.Optimization;

namespace Domna.Domain.Interfaces;

/// <summary>
/// Solves the inverse problem: searches the fitted region for the parameters that
/// reproduce a wanted temperature profile.
/// </summary>
/// <remarks>
/// Implementations are deterministic — the same request yields the same outcome —
/// but not instantaneous, which is why the call takes a cancellation token and
/// reports progress. The caller runs it off the UI thread.
/// </remarks>
public interface IParameterOptimizer
{
    /// <summary>Searches for the parameters that best match <paramref name="request"/>.</summary>
    /// <param name="request">Start point, target profile and the free parameters.</param>
    /// <param name="progress">Called once per finished round, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the search; the best point so far is still returned.</param>
    OptimizationOutcome Optimize(
        OptimizationRequest request,
        Action<OptimizationProgress>? progress,
        CancellationToken cancellationToken);
}
