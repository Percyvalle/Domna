using Domna.Domain.Models;
using Domna.Domain.Parameters;

namespace Domna.Calculation;

/// <summary>
/// The monomials the three fitted expansions are built from — cubes of single
/// parameters and pairwise products of a parameter with a charge-distribution
/// station reading.
/// </summary>
/// <remarks>
/// Computed once per calculation and shared by all three expansions: the same
/// monomial appears in two or three of them, and recomputing it per expansion
/// would be both slower and a place for the definitions to drift apart.
/// </remarks>
internal readonly struct RegressionTerms
{
    internal RegressionTerms(ParameterSet p)
    {
        double burdenLevel = p[Parameter.BurdenLevel];
        double oreLoadRate = p[Parameter.OreLoadRate];
        double cokeSize = p[Parameter.CokeSize];
        double sinterSize = p[Parameter.SinterSize];
        double pelletsSize = p[Parameter.PelletsSize];
        double pelletsRatio = p[Parameter.PelletsRatio];
        double stations9To11 = p[Parameter.ChargeStations9To11];
        double stations6To8 = p[Parameter.ChargeStations6To8];
        double stations3To5 = p[Parameter.ChargeStations3To5];
        double hotBlastPressure = p[Parameter.HotBlastPressure];
        double topGasPressure = p[Parameter.TopGasPressure];

        double burdenLevelSq = burdenLevel * burdenLevel;
        double oreLoadRateSq = oreLoadRate * oreLoadRate;

        BurdenLevelCubed = burdenLevelSq * burdenLevel;
        OreLoadRateCubed = oreLoadRateSq * oreLoadRate;
        CokeSizeCubed = cokeSize * cokeSize * cokeSize;
        PelletsRatioCubed = pelletsRatio * pelletsRatio * pelletsRatio;
        Stations9To11Cubed = stations9To11 * stations9To11 * stations9To11;
        Stations6To8Cubed = stations6To8 * stations6To8 * stations6To8;
        Stations3To5Cubed = stations3To5 * stations3To5 * stations3To5;
        HotBlastPressureCubed = hotBlastPressure * hotBlastPressure * hotBlastPressure;
        TopGasPressureCubed = topGasPressure * topGasPressure * topGasPressure;

        BurdenLevelSqBy9To11 = burdenLevelSq * stations9To11;
        BurdenLevelSqBy6To8 = burdenLevelSq * stations6To8;
        BurdenLevelSqBy3To5 = burdenLevelSq * stations3To5;

        OreLoadRateSqBy9To11 = oreLoadRateSq * stations9To11;
        OreLoadRateSqBy6To8 = oreLoadRateSq * stations6To8;
        OreLoadRateSqBy3To5 = oreLoadRateSq * stations3To5;

        CokeSizeBy9To11Sq = cokeSize * stations9To11 * stations9To11;
        CokeSizeBy6To8Sq = cokeSize * stations6To8 * stations6To8;
        CokeSizeBy3To5Sq = cokeSize * stations3To5 * stations3To5;

        PelletsSizeBy9To11 = pelletsSize * stations9To11;
        PelletsSizeBy6To8 = pelletsSize * stations6To8;
        PelletsSizeBy3To5 = pelletsSize * stations3To5;

        SinterSizeBy9To11 = sinterSize * stations9To11;
        SinterSizeBy6To8 = sinterSize * stations6To8;
        SinterSizeBy3To5 = sinterSize * stations3To5;

        HotBlastPressureBy9To11Sq = hotBlastPressure * stations9To11 * stations9To11;
    }

    internal double BurdenLevelCubed { get; }

    internal double OreLoadRateCubed { get; }

    internal double CokeSizeCubed { get; }

    internal double PelletsRatioCubed { get; }

    internal double Stations9To11Cubed { get; }

    internal double Stations6To8Cubed { get; }

    internal double Stations3To5Cubed { get; }

    internal double HotBlastPressureCubed { get; }

    internal double TopGasPressureCubed { get; }

    internal double BurdenLevelSqBy9To11 { get; }

    internal double BurdenLevelSqBy6To8 { get; }

    internal double BurdenLevelSqBy3To5 { get; }

    internal double OreLoadRateSqBy9To11 { get; }

    internal double OreLoadRateSqBy6To8 { get; }

    internal double OreLoadRateSqBy3To5 { get; }

    internal double CokeSizeBy9To11Sq { get; }

    internal double CokeSizeBy6To8Sq { get; }

    internal double CokeSizeBy3To5Sq { get; }

    internal double PelletsSizeBy9To11 { get; }

    internal double PelletsSizeBy6To8 { get; }

    internal double PelletsSizeBy3To5 { get; }

    internal double SinterSizeBy9To11 { get; }

    internal double SinterSizeBy6To8 { get; }

    internal double SinterSizeBy3To5 { get; }

    internal double HotBlastPressureBy9To11Sq { get; }
}
