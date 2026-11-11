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

        double[] start;
        Func<double[], ParameterSet> decode;
        Func<double[], ChargeProgram?> decodeProgram;

        if (request.StationMode is ChargeStationMode.Matrix { Program: var program })
        {
            (start, decode, decodeProgram) = BuildMatrixSearch(request, program);
        }
        else
        {
            (start, decode) = BuildContinuousSearch(request);
            decodeProgram = _ => null;
        }

        // Nothing is free: the answer is the question. Reported as a finished search
        // carrying the start point's residual, so the screen can still say how far off it is.
        if (start.Length == 0)
        {
            return new OptimizationOutcome(request.Start, objective.At(request.Start), 0, cancellationToken.IsCancellationRequested);
        }

        double[] best = start;
        double bestScore = objective.At(decode(start));
        int roundsRun = 0;

        for (int round = 0; round < Rounds && !cancellationToken.IsCancellationRequested; round++)
        {
            SearchCandidates candidates = new();
            double Evaluate(double[] point)
            {
                double score = objective.At(decode(point));
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
            Parameters: decode(best),
            Residual: bestScore,
            Rounds: roundsRun,
            Cancelled: cancellationToken.IsCancellationRequested,
            UpdatedChargeProgram: decodeProgram(best));
    }

    private static (double[] Start, Func<double[], ParameterSet> Decode) BuildContinuousSearch(
        OptimizationRequest request)
    {
        Parameter[] free = ChargeStationBalance.MovableParameters(request);
        if (free.Length == 0)
        {
            return ([], _ => request.Start);
        }

        double[] start = Coordinates(request.Start, free);
        return (start, coords => Materialize(coords, request, free));
    }

    private static (double[] Start, Func<double[], ParameterSet> Decode, Func<double[], ChargeProgram?> DecodeProgram) BuildMatrixSearch(
        OptimizationRequest request, ChargeProgram program)
    {
        int[] freeZoneBuckets = GetFreeZoneBuckets(request.FreeParameters);

        IMatrixConstraint matrix = request.StationMode is ChargeStationMode.Matrix { MatrixMode: MatrixOptimizerMode.Aggregate }
            ? new AggregateStationConstraint(program, freeZoneBuckets)
            : new MatrixStationConstraint(program, freeZoneBuckets);

        Parameter[] freeNonStation = [.. request.FreeParameters.Distinct().Where(IsNonStation)];

        // Nothing moves: no non-station params are free and the matrix has
        // nothing to redistribute between zones.
        if (freeNonStation.Length == 0 && !matrix.IsMovable)
        {
            return ([], _ => request.Start, _ => null);
        }

        double[] matrixStart = matrix.IsMovable ? matrix.Encode() : [];
        double[] nonStationStart = Coordinates(request.Start, freeNonStation);
        double[] start = [.. nonStationStart, .. matrixStart];

        Func<double[], ParameterSet> decode = coords =>
        {
            ParameterSet ps = ApplyCoordinates(coords, request.Start, freeNonStation);
            return matrix.IsMovable
                ? matrix.Decode(ps, coords.AsSpan(freeNonStation.Length))
                : ps;
        };

        Func<double[], ChargeProgram?> decodeProgram = matrix.IsMovable
            ? coords => matrix.BuildUpdatedProgram(program, coords.AsSpan(freeNonStation.Length))
            : _ => null;

        return (start, decode, decodeProgram);
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

    private static bool IsNonStation(Parameter p) => p is not (
        Parameter.ChargeStations9To11 or Parameter.ChargeStations6To8 or Parameter.ChargeStations3To5);

    /// <summary>
    /// Returns the sorted bucket indices (1, 2, 3) for the zone parameters in
    /// <paramref name="freeParameters"/>. When none are present, defaults to all three
    /// zones so that existing matrix-mode requests without explicit zone selection
    /// continue to redistribute across the full range.
    /// </summary>
    private static int[] GetFreeZoneBuckets(IReadOnlyList<Parameter> freeParameters)
    {
        int[] buckets = [.. freeParameters
            .Where(p => p is Parameter.ChargeStations3To5 or Parameter.ChargeStations6To8 or Parameter.ChargeStations9To11)
            .Select(p => p switch
            {
                Parameter.ChargeStations3To5 => 1,
                Parameter.ChargeStations6To8 => 2,
                _ => 3,
            })
            .Distinct()
            .OrderBy(b => b)];
        return buckets.Length == 0 ? [1, 2, 3] : buckets;
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

    /// <summary>Writes the given parameters from their normalized coordinates into the start set.</summary>
    private static ParameterSet ApplyCoordinates(double[] coordinates, ParameterSet start, Parameter[] free)
    {
        ParameterSet parameters = start;
        for (int d = 0; d < free.Length; d++)
        {
            ParameterRange range = ParameterCatalog.RangeOf(free[d]);
            parameters = parameters.With(free[d], range.Minimum + (coordinates[d] * (range.Maximum - range.Minimum)));
        }

        return parameters;
    }

    /// <summary>The start set with the free parameters moved to <paramref name="coordinates"/>.</summary>
    private static ParameterSet Materialize(double[] coordinates, OptimizationRequest request, Parameter[] free)
    {
        ParameterSet parameters = ApplyCoordinates(coordinates, request.Start, free);
        return ChargeStationBalance.Restore(parameters, request.Start, free);
    }
}
