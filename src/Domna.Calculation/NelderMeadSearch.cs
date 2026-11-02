namespace Domna.Calculation;

internal static class NelderMeadSearch
{
    private const double NmStep = 0.02;
    private const double NmDiameterTolerance = 1e-10;

    /// <summary>Refines <paramref name="start"/> with a Nelder-Mead simplex, staying in the fitted region.</summary>
    /// <remarks>
    /// Works in the same normalised [0, 1]^n space as DE. Vertices that would leave the
    /// box are clamped back in, which can collapse a simplex near a boundary.
    /// Convergence is declared when the diameter of the simplex drops below
    /// <see cref="NmDiameterTolerance"/> in normalised space.
    /// </remarks>
    public static (double[] Coordinates, double Score) Refine(
        double[] start,
        double startScore,
        Func<double[], double> evaluate,
        CancellationToken cancellationToken)
    {
        int n = start.Length;

        double[][] simplex = new double[n + 1][];
        double[] scores = new double[n + 1];

        simplex[0] = (double[])start.Clone();
        scores[0] = startScore;

        for (int i = 0; i < n; i++)
        {
            double[] vertex = (double[])start.Clone();
            double step = vertex[i] <= (1.0 - NmStep) ? NmStep : -NmStep;
            vertex[i] += step;
            simplex[i + 1] = vertex;
            scores[i + 1] = evaluate(vertex);
        }

        double[] centroid = new double[n];

        for (int iter = 0; iter < (100 * n); iter++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            Array.Sort(scores, simplex);

            // Convergence: stop when the simplex has shrunk to numerical noise.
            double diameter = 0.0;
            for (int i = 1; i <= n; i++)
            {
                for (int d = 0; d < n; d++)
                {
                    double delta = Math.Abs(simplex[i][d] - simplex[0][d]);
                    if (delta > diameter)
                    {
                        diameter = delta;
                    }
                }
            }

            if (diameter < NmDiameterTolerance)
            {
                break;
            }

            // Centroid of all vertices except the worst.
            Array.Clear(centroid);
            for (int i = 0; i < n; i++)
            {
                for (int d = 0; d < n; d++)
                {
                    centroid[d] += simplex[i][d] / n;
                }
            }

            double[] worst = simplex[n];

            // Reflection: x_r = centroid + (centroid - worst).
            double[] reflected = new double[n];
            for (int d = 0; d < n; d++)
            {
                reflected[d] = Math.Clamp((2.0 * centroid[d]) - worst[d], 0.0, 1.0);
            }

            double reflectedScore = evaluate(reflected);

            if (reflectedScore < scores[0])
            {
                // Expansion: x_e = centroid + 2*(reflected - centroid).
                double[] expanded = new double[n];
                for (int d = 0; d < n; d++)
                {
                    expanded[d] = Math.Clamp((2.0 * reflected[d]) - centroid[d], 0.0, 1.0);
                }

                double expandedScore = evaluate(expanded);

                if (expandedScore < reflectedScore)
                {
                    simplex[n] = expanded;
                    scores[n] = expandedScore;
                }
                else
                {
                    simplex[n] = reflected;
                    scores[n] = reflectedScore;
                }
            }
            else if (reflectedScore < scores[n - 1])
            {
                simplex[n] = reflected;
                scores[n] = reflectedScore;
            }
            else
            {
                // Contraction.
                double[] contracted = new double[n];
                bool accepted;

                if (reflectedScore < scores[n])
                {
                    // Outside contraction: midpoint of centroid and reflected.
                    for (int d = 0; d < n; d++)
                    {
                        contracted[d] = Math.Clamp(centroid[d] + (0.5 * (reflected[d] - centroid[d])), 0.0, 1.0);
                    }

                    double contractedScore = evaluate(contracted);
                    accepted = contractedScore <= reflectedScore;
                    if (accepted)
                    {
                        simplex[n] = contracted;
                        scores[n] = contractedScore;
                    }
                }
                else
                {
                    // Inside contraction: midpoint of centroid and worst.
                    for (int d = 0; d < n; d++)
                    {
                        contracted[d] = Math.Clamp(centroid[d] + (0.5 * (worst[d] - centroid[d])), 0.0, 1.0);
                    }

                    double contractedScore = evaluate(contracted);
                    accepted = contractedScore < scores[n];
                    if (accepted)
                    {
                        simplex[n] = contracted;
                        scores[n] = contractedScore;
                    }
                }

                if (!accepted)
                {
                    // Shrink: all vertices move halfway toward the best.
                    double[] best = simplex[0];
                    for (int i = 1; i <= n; i++)
                    {
                        for (int d = 0; d < n; d++)
                        {
                            simplex[i][d] = Math.Clamp(best[d] + (0.5 * (simplex[i][d] - best[d])), 0.0, 1.0);
                        }

                        scores[i] = evaluate(simplex[i]);
                    }
                }
            }
        }

        // The final sort may not have run; scan to find the true best.
        int bestIndex = 0;
        for (int i = 1; i <= n; i++)
        {
            if (scores[i] < scores[bestIndex])
            {
                bestIndex = i;
            }
        }

        return (simplex[bestIndex], scores[bestIndex]);
    }
}
