namespace Domna.Domain.Optimization;

/// <summary>
/// One progress report from a running search, so the screen can show a round
/// counter instead of an unbounded spinner.
/// </summary>
/// <param name="Round">One-based index of the round just finished.</param>
/// <param name="TotalRounds">How many rounds the search will run at most.</param>
/// <param name="Residual">Best objective value found so far.</param>
public readonly record struct OptimizationProgress(int Round, int TotalRounds, double Residual);
