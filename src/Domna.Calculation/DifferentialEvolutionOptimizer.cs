using Domna.Domain.Interfaces;
using Domna.Domain.Models;
using Domna.Domain.Optimization;
using Domna.Domain.Parameters;

namespace Domna.Calculation;

/// <summary>
/// Solves the inverse problem by differential evolution over the free parameters,
/// followed by a Nelder-Mead simplex polish.
/// </summary>
/// <remarks>
/// <para>
/// The search runs in several rounds. Each round is a DE/rand/1/bin population drawn
/// uniformly across the whole fitted region from an independent fixed seed. Three
/// promising, separated candidates from each round receive a Nelder-Mead polish.
/// The best point from all evaluations is retained, including on cancellation.
/// </para>
/// <para>
/// The search never leaves the fitted region. Its bounds are the region the regression
/// was fitted in, so a parameter set found outside them would be an extrapolated answer
/// to a question the operator asked inside the data.
/// </para>
/// <para>
/// Deterministic: the same request returns the same outcome, because the population is
/// drawn from <see cref="SearchSequence"/> seeded by a constant. Cancellation is the one
/// input that is not the request - a cancelled search returns the best point found so
/// far, flagged as cancelled, never a partial or invented one.
/// </para>
/// </remarks>
public sealed class DifferentialEvolutionOptimizer : IParameterOptimizer
{
    private const int Rounds = 4;
    private const int GenerationsPerRound = 90;
    private const ulong SeedStride = 0x9E3779B97F4A7C15UL;
    private const ulong SearchSeed = 0x646F6D6E61UL;

    private readonly ITemperatureProfileCalculator _calculator;

    public DifferentialEvolutionOptimizer(ITemperatureProfileCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(calculator);

        _calculator = calculator;
    }

    /// <inheritdoc />
    public OptimizationOutcome Optimize(
        OptimizationRequest request,
        Action<OptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        RatioObjective objective = new(_calculator, request.Target);
        Parameter[] free = ChargeStationBalance.MovableParameters(request);

        // Nothing is free: the answer is the question. Reported as a finished search
        // carrying the start point's residual, so the screen can still say how far off it is.
        if (free.Length == 0)
        {
            return new OptimizationOutcome(request.Start, objective.At(request.Start), 0, cancellationToken.IsCancellationRequested);
        }

        double[] start = Coordinates(request.Start, free);
        double[] best = start;
        double bestScore = objective.At(Materialize(start, request, free));
        int roundsRun = 0;

        for (int round = 0; round < Rounds && !cancellationToken.IsCancellationRequested; round++)
        {
            SearchCandidates candidates = new();
            double Evaluate(double[] point)
            {
                double score = objective.At(Materialize(point, request, free));
                candidates.Consider(point, score);
                if (score < bestScore)
                {
                    best = (double[])point.Clone();
                    bestScore = score;
                }

                return score;
            }

            SearchSequence sequence = new(unchecked(SearchSeed + ((ulong)round * SeedStride)));
            SearchRound(start, Evaluate, candidates, sequence, request.Settings, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                roundsRun++;
                progress?.Invoke(new OptimizationProgress(roundsRun, Rounds, bestScore));
            }
        }

        return new OptimizationOutcome(
            Materialize(best, request, free), bestScore, roundsRun, cancellationToken.IsCancellationRequested);
    }

    private static void SearchRound(
        double[] start,
        Func<double[], double> evaluate,
        SearchCandidates candidates,
        SearchSequence sequence,
        SearchSettings settings,
        CancellationToken cancellationToken)
    {
        DifferentialEvolutionPopulation population = new(start, evaluate, sequence, cancellationToken);
        for (int generation = 0; generation < GenerationsPerRound && !cancellationToken.IsCancellationRequested; generation++)
        {
            population.Evolve(evaluate, sequence, settings, cancellationToken);
        }

        // Retain distinct basins from the entire search, before the population collapses.
        foreach ((double[] point, double score) in candidates.Points.ToArray())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            (double[] refined, double refinedScore) = NelderMeadSearch.Refine(point, score, evaluate, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                // Clamping can flatten a simplex at a boundary; rebuild it around the refined point.
                NelderMeadSearch.Refine(refined, refinedScore, evaluate, cancellationToken);
            }
        }
    }

    /// <summary>The free parameters of <paramref name="parameters"/> as fractions of their range.</summary>
    private static double[] Coordinates(ParameterSet parameters, Parameter[] free)
    {
        double[] coordinates = new double[free.Length];
        for (int d = 0; d < free.Length; d++)
        {
            ParameterRange range = ParameterCatalog.RangeOf(free[d]);
            double span = range.Maximum - range.Minimum;
            coordinates[d] = span == 0.0 ? 0.0 : Math.Clamp((parameters[free[d]] - range.Minimum) / span, 0.0, 1.0);
        }

        return coordinates;
    }

    /// <summary>The start set with the free parameters moved to <paramref name="coordinates"/>.</summary>
    private static ParameterSet Materialize(double[] coordinates, OptimizationRequest request, Parameter[] free)
    {
        ParameterSet parameters = request.Start;
        for (int d = 0; d < free.Length; d++)
        {
            ParameterRange range = ParameterCatalog.RangeOf(free[d]);
            parameters = parameters.With(free[d], range.Minimum + (coordinates[d] * (range.Maximum - range.Minimum)));
        }

        return ChargeStationBalance.Restore(parameters, request.Start, free);
    }
}
