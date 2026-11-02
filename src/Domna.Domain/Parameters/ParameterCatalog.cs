namespace Domna.Domain.Parameters;

/// <summary>
/// Bounds, defaults and display precision for every <see cref="Parameter"/>.
/// </summary>
/// <remarks>
/// These are measured process limits, not UI preferences: the regression was
/// fitted inside this region and says nothing outside it. Never widen a bound to
/// make an input "work" — an out-of-range value is a finding to show the operator.
/// </remarks>
public static class ParameterCatalog
{
    /// <summary>Number of parameters the model takes — the length of the parameter vector.</summary>
    public const int Count = 11;

    private static readonly ParameterRange[] RangesByIndex =
    [
        new(1.25, 3.10, 1.857, 3),      // BurdenLevel
        new(3.72, 4.57, 3.880, 3),      // OreLoadRate
        new(50.00, 57.70, 53.88, 2),    // CokeSize
        new(9.74, 11.69, 10.31, 2),     // SinterSize
        new(12.68, 14.61, 13.54, 2),    // PelletsSize
        new(0.238, 0.505, 0.342, 3),    // PelletsRatio
        new(59.70, 100.00, 75.00, 1),   // ChargeStations9To11
        new(43.75, 58.82, 50.00, 2),    // ChargeStations6To8
        new(38.83, 42.55, 40.00, 2),    // ChargeStations3To5
        new(2.45, 2.80, 2.661, 3),      // HotBlastPressure
        new(1.37, 1.48, 1.399, 3),      // TopGasPressure
    ];

    /// <summary>Every parameter, in parameter-vector order.</summary>
    public static IReadOnlyList<Parameter> All { get; } = Enum.GetValues<Parameter>();

    /// <summary>The range of one parameter.</summary>
    public static ParameterRange RangeOf(Parameter parameter) => RangesByIndex[(int)parameter];
}
