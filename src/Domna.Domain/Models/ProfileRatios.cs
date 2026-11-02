namespace Domna.Domain.Models;

/// <summary>
/// The three temperature ratios the operator reads off a profile.
/// </summary>
/// <remarks>
/// A ratio describes the <em>shape</em> of the gas flow, which is what the charge
/// program steers; the absolute level mostly follows the heat balance. The first
/// two are fitted directly by the regression, the third is derived from them —
/// see <c>docs/domain-model.md</c>.
/// </remarks>
/// <param name="CenterToOreRidge">Тц/Тгр — centre against the ore ridge.</param>
/// <param name="PeripheryToCenter">Тпер/Тц — periphery against the centre.</param>
/// <param name="PeripheryToOreRidge">Тпер/Тгр — derived from the other two.</param>
public readonly record struct ProfileRatios(
    double CenterToOreRidge,
    double PeripheryToCenter,
    double PeripheryToOreRidge)
{
    /// <summary>The ratios of one profile.</summary>
    public static ProfileRatios Of(TemperatureProfile profile) => new(
        profile.Ratio(TemperaturePosition.Center, TemperaturePosition.OreRidge),
        profile.Ratio(TemperaturePosition.Periphery, TemperaturePosition.Center),
        profile.Ratio(TemperaturePosition.Periphery, TemperaturePosition.OreRidge));
}
