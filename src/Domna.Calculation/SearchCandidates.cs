namespace Domna.Calculation;

internal sealed class SearchCandidates
{
    private const int Capacity = 3;
    private const double Separation = 0.1;

    private readonly List<(double[] Coordinates, double Score)> _points = [];

    public IReadOnlyList<(double[] Coordinates, double Score)> Points => _points;

    public void Consider(double[] coordinates, double score)
    {
        if (_points.Count == Capacity && score >= _points[^1].Score)
        {
            return;
        }

        // Keep an alternative basin even when its unpolished score is worse.
        foreach ((double[] point, double previousScore) in _points)
        {
            if (Distance(point, coordinates) < Separation && previousScore <= score)
            {
                return;
            }
        }

        _points.RemoveAll(point => Distance(point.Coordinates, coordinates) < Separation);
        _points.Add(((double[])coordinates.Clone(), score));
        _points.Sort((left, right) => left.Score.CompareTo(right.Score));
        if (_points.Count > Capacity)
        {
            _points.RemoveAt(Capacity);
        }
    }

    private static double Distance(double[] left, double[] right)
    {
        double distance = 0.0;
        for (int d = 0; d < left.Length; d++)
        {
            distance = Math.Max(distance, Math.Abs(left[d] - right[d]));
        }

        return distance;
    }
}
