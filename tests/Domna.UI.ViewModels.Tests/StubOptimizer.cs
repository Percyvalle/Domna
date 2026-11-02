using Domna.Domain.Interfaces;
using Domna.Domain.Models;
using Domna.Domain.Optimization;

namespace Domna.UI.ViewModels.Tests;

/// <summary>An optimizer that records what it was asked and answers instantly.</summary>
internal sealed class StubOptimizer : IParameterOptimizer
{
    public ParameterSet? Answer { get; set; }

    public OptimizationRequest? LastRequest { get; private set; }

    public OptimizationOutcome Optimize(
        OptimizationRequest request,
        Action<OptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        progress?.Invoke(new OptimizationProgress(1, 1, 0.0));

        return new OptimizationOutcome(Answer ?? request.Start, 0.0, 1, false);
    }
}
