namespace Domna.Domain.Optimization;

/// <summary>
/// Tuning knobs passed to <see cref="Domna.Domain.Interfaces.IParameterOptimizer"/>
/// at search time. The same settings are used in each independent search round.
/// </summary>
public sealed record SearchSettings
{
    /// <summary>Mutation scaling factor (DE parameter F).</summary>
    public double MutationWeight { get; init; } = 0.6;

    /// <summary>Binomial crossover probability (DE parameter CR).</summary>
    public double CrossoverRate { get; init; } = 0.9;
}
