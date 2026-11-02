using Domna.Domain.Optimization;

namespace Domna.Calculation;

internal sealed class DifferentialEvolutionPopulation
{
    private readonly double[][] _points;
    private readonly double[] _scores;

    public DifferentialEvolutionPopulation(
        double[] start,
        Func<double[], double> evaluate,
        SearchSequence sequence,
        CancellationToken cancellationToken)
    {
        int size = Math.Clamp(8 * start.Length, 24, 72);
        _points = new double[size][];
        _scores = new double[size];
        for (int i = 0; i < size && !cancellationToken.IsCancellationRequested; i++)
        {
            double[] point = i == 0 ? (double[])start.Clone() : Sample(start.Length, sequence);
            _points[i] = point;
            _scores[i] = evaluate(point);
        }
    }

    public void Evolve(
        Func<double[], double> evaluate,
        SearchSequence sequence,
        SearchSettings settings,
        CancellationToken cancellationToken)
    {
        double[] trial = new double[_points[0].Length];
        for (int i = 0; i < _points.Length && !cancellationToken.IsCancellationRequested; i++)
        {
            Mutate(trial, i, sequence, settings);
            double score = evaluate(trial);
            if (score < _scores[i])
            {
                trial.CopyTo(_points[i], 0);
                _scores[i] = score;
            }
        }
    }

    private static double[] Sample(int dimensions, SearchSequence sequence)
    {
        double[] point = new double[dimensions];
        for (int d = 0; d < dimensions; d++)
        {
            point[d] = sequence.NextDouble();
        }

        return point;
    }

    private static int Other(SearchSequence sequence, int size, int x, int y, int z)
    {
        int index;
        do
        {
            index = sequence.NextIndex(size);
        }
        while (index == x || index == y || index == z);

        return index;
    }

    private void Mutate(double[] trial, int index, SearchSequence sequence, SearchSettings settings)
    {
        int size = _points.Length;
        int a = Other(sequence, size, index, index, index);
        int b = Other(sequence, size, index, a, a);
        int c = Other(sequence, size, index, a, b);
        int forced = sequence.NextIndex(trial.Length);
        for (int d = 0; d < trial.Length; d++)
        {
            trial[d] = d == forced || sequence.NextDouble() < settings.CrossoverRate
                ? Math.Clamp(_points[a][d] + (settings.MutationWeight * (_points[b][d] - _points[c][d])), 0.0, 1.0)
                : _points[index][d];
        }
    }
}
