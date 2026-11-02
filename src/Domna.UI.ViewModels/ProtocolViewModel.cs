using System.Globalization;
using Domna.Domain.Models;

namespace Domna.UI.ViewModels;

/// <summary>
/// The printable record of one calculation: what was entered, what came out, and
/// whether the model was asked to answer outside the data it was fitted on.
/// </summary>
/// <remarks>
/// Immutable and built on demand. A protocol describes the calculation that was on
/// screen when it was opened, so it must not follow later edits to the inputs.
/// </remarks>
public sealed class ProtocolViewModel
{
    private ProtocolViewModel(
        string stamp,
        IReadOnlyList<ProtocolRow> rows,
        IReadOnlyList<ProtocolReading> readings,
        string ratiosCaption,
        string regionCaption)
    {
        Stamp = stamp;
        Rows = rows;
        Readings = readings;
        RatiosCaption = ratiosCaption;
        RegionCaption = regionCaption;
    }

    public string Stamp { get; }

    public IReadOnlyList<ProtocolRow> Rows { get; }

    public IReadOnlyList<ProtocolReading> Readings { get; }

    public string RatiosCaption { get; }

    /// <summary>Whether the result is an extrapolation, in words.</summary>
    public string RegionCaption { get; }

    /// <summary>Builds the protocol of one finished calculation.</summary>
    /// <param name="inputs">The input list the calculation was run from.</param>
    /// <param name="profile">The computed profile.</param>
    /// <param name="sequenceNumber">The calculation's number within this session.</param>
    /// <param name="stampedAt">When the protocol was opened.</param>
    public static ProtocolViewModel Of(
        IEnumerable<ParameterInputViewModel> inputs,
        TemperatureProfile profile,
        int sequenceNumber,
        DateTimeOffset stampedAt)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        ParameterInputViewModel[] list = [.. inputs];
        ProfileRatios ratios = ProfileRatios.Of(profile);

        string stamp = string.Create(
            CultureInfo.CurrentCulture,
            $"{stampedAt.LocalDateTime:dd.MM.yyyy} · Расчёт #{sequenceNumber:000}");

        string ratiosCaption = string.Join(
            " · ",
            $"Тц/Тгр = {DisplayNumber.Ratio(ratios.CenterToOreRidge)}",
            $"Тпер/Тц = {DisplayNumber.Ratio(ratios.PeripheryToCenter)}",
            $"Тпер/Тгр = {DisplayNumber.Ratio(ratios.PeripheryToOreRidge)}");

        ProtocolRow[] rows = [.. list.Select(input => new ProtocolRow(
            input.ShortName,
            DisplayNumber.Fixed(input.Value, input.Range.Decimals),
            input.Unit))];

        ProtocolReading[] readings =
        [
            new("Тц · центр", DisplayNumber.Temperature(profile.Center)),
            new("Тгр · рудный гребень", DisplayNumber.Temperature(profile.OreRidge)),
            new("Тпер · периферия", DisplayNumber.Temperature(profile.Periphery)),
        ];

        string regionCaption = list.Any(input => input.IsOutOfRange)
            ? "⚠ Результат — экстраполяция за пределы области обучения."
            : "Все параметры внутри области обучения модели.";

        return new ProtocolViewModel(stamp, rows, readings, ratiosCaption, regionCaption);
    }
}
