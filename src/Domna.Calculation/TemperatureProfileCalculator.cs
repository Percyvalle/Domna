using Domna.Domain.Interfaces;
using Domna.Domain.Models;

namespace Domna.Calculation;

/// <summary>
/// The fitted thermal model of the furnace top.
/// </summary>
/// <remarks>
/// <para>
/// Three quantities are fitted directly — the centre temperature and two
/// temperature ratios — and the remaining temperatures are derived from them.
/// The ratios are what the fit is good at: they describe the shape of the gas
/// flow, which is what the charge program actually controls.
/// </para>
/// <para>
/// The float literals below are regression coefficients: measured domain data,
/// carried at full double precision and in the fitted term order. They are not
/// magic numbers to be tidied, rounded, renamed or reordered. The layout — one
/// term per line, coefficient first — is deliberate: it keeps the expansions
/// diffable against the reference implementation they were ported from.
/// </para>
/// </remarks>
public sealed class TemperatureProfileCalculator : ITemperatureProfileCalculator
{
    /// <inheritdoc />
    public TemperatureProfile Calculate(ParameterSet parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        RegressionTerms terms = new(parameters);

        double center = CenterTemperature(terms);
        double centerToRidgeRatio = CenterToRidgeRatio(terms);
        double peripheryToCenterRatio = PeripheryToCenterRatio(terms);

        // A zero ratio means the parameters left the fitted region entirely; report
        // a flat zero rather than an infinity that would poison every downstream ratio.
        double oreRidge = centerToRidgeRatio == 0.0 ? 0.0 : center / centerToRidgeRatio;
        double periphery = center * peripheryToCenterRatio;

        return new TemperatureProfile(center, oreRidge, periphery);
    }

    /// <summary>Gas temperature on the furnace axis, degrees Celsius.</summary>
    private static double CenterTemperature(in RegressionTerms m) =>
          2.5589359153118494
        + (30.344583916211743 * m.BurdenLevelCubed)
        + (525.62763840243929 * m.PelletsRatioCubed)
        + (-13.088038540243385 * m.OreLoadRateCubed)
        + (3.596539618219213e-3 * m.CokeSizeCubed)
        + (-6.7375939759587375e-3 * m.CokeSizeBy6To8Sq)
        + (-1.6416954891413955 * m.PelletsSizeBy9To11)
        + (-0.80856557703749732 * m.SinterSizeBy6To8)
        + (0.42169576371857914 * m.BurdenLevelSqBy9To11)
        + (-1.0131682845853269 * m.BurdenLevelSqBy6To8)
        + (-2.5750766987637301 * m.BurdenLevelSqBy3To5)
        + (0.82060366438825971 * m.OreLoadRateSqBy9To11)
        + (3.3735859470460121 * m.OreLoadRateSqBy6To8)
        + (-1.6962036191501393e-4 * m.Stations9To11Cubed)
        + (4.222433923448371e-3 * m.Stations6To8Cubed)
        + (-4.1688758256366292e-3 * m.Stations3To5Cubed)
        + (-1.5297517840328191e-3 * m.CokeSizeBy9To11Sq)
        + (-3.5112746523071361 * m.OreLoadRateSqBy3To5)
        + (-2.2627815350756308 * m.PelletsSizeBy6To8)
        + (6.5375963728987729 * m.PelletsSizeBy3To5)
        + (0.63818307258525009 * m.SinterSizeBy9To11)
        + (-17.579614211097006 * m.HotBlastPressureCubed)
        + (-129.85249700059418 * m.TopGasPressureCubed)
        + (5.1851563872303275e-2 * m.HotBlastPressureBy9To11Sq);

    /// <summary>Ratio of the centre temperature to the ore-ridge temperature.</summary>
    private static double CenterToRidgeRatio(in RegressionTerms m) =>
          4.276467629217759
        + (0.31409054987210466 * m.BurdenLevelCubed)
        + (-7.4421166184140191e-5 * m.CokeSizeCubed)
        + (2.1636350399128496e-2 * m.SinterSizeBy6To8)
        + (5.9796411061149579e-3 * m.BurdenLevelSqBy6To8)
        + (4.2952166919878694e-2 * m.OreLoadRateSqBy6To8)
        + (-5.1244176073939941e-4 * m.Stations3To5Cubed)
        + (0.13003899919443621 * m.PelletsSizeBy3To5)
        + (8.4715847354967426 * m.PelletsRatioCubed)
        + (-4.5084336721329378e-5 * m.CokeSizeBy6To8Sq)
        + (-2.0094424805036325e-2 * m.SinterSizeBy3To5)
        + (-6.252006906301795e-2 * m.BurdenLevelSqBy3To5)
        + (1.2595613859858956e-5 * m.Stations9To11Cubed)
        + (-2.304323693243536e-2 * m.OreLoadRateSqBy3To5)
        + (-3.5213851342220902e-2 * m.PelletsSizeBy9To11)
        + (-0.30705609483777679 * m.OreLoadRateCubed)
        + (4.2867160771064709e-4 * m.CokeSizeBy3To5Sq)
        + (1.2218355151957509e-2 * m.BurdenLevelSqBy9To11)
        + (7.3447090545015973e-3 * m.OreLoadRateSqBy9To11)
        + (-2.1430854818439689e-5 * m.Stations6To8Cubed)
        + (-4.5772202176303102e-2 * m.PelletsSizeBy6To8)
        + (3.694622582316387e-2 * m.HotBlastPressureCubed)
        + (-1.9631361222814314 * m.TopGasPressureCubed);

    /// <summary>Ratio of the periphery temperature to the centre temperature.</summary>
    private static double PeripheryToCenterRatio(in RegressionTerms m) =>
          1.2822512475902053
        + (-1.5074462082603681e-2 * m.BurdenLevelCubed)
        + (-3.8373318035069441e-6 * m.CokeSizeCubed)
        + (-5.0863137659456206e-3 * m.SinterSizeBy6To8)
        + (3.9318619317355464e-3 * m.BurdenLevelSqBy6To8)
        + (4.0147871451624671e-4 * m.OreLoadRateSqBy6To8)
        + (-1.0235860766247799e-6 * m.Stations3To5Cubed)
        + (-3.5918321582119176e-3 * m.SinterSizeBy9To11)
        + (-1.5113330554904651 * m.PelletsRatioCubed)
        + (4.4992082507091869e-6 * m.CokeSizeBy6To8Sq)
        + (1.1118373962503798e-2 * m.SinterSizeBy3To5)
        + (-2.3254351873675397e-3 * m.BurdenLevelSqBy3To5)
        + (-9.6507082511991406e-7 * m.Stations9To11Cubed)
        + (1.3382703609620992e-6 * m.CokeSizeBy9To11Sq)
        + (-9.3123644222985e-3 * m.OreLoadRateSqBy3To5)
        + (2.840707724224012e-2 * m.OreLoadRateCubed)
        + (1.2839250395169832e-3 * m.PelletsSizeBy9To11)
        + (-2.4969666375056242e-4 * m.BurdenLevelSqBy9To11)
        + (2.236315691601814e-3 * m.OreLoadRateSqBy9To11)
        + (7.405880508745963e-6 * m.Stations6To8Cubed)
        + (-2.4126279673315301e-3 * m.PelletsSizeBy6To8)
        + (-1.5267807092068842e-2 * m.HotBlastPressureCubed)
        + (0.32699653049583777 * m.TopGasPressureCubed);
}
