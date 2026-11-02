using System.Globalization;
using System.Text;
using Domna.Domain.Models;
using Domna.Domain.Parameters;

namespace Domna.UI.ViewModels;

/// <summary>
/// The session history rendered as one delimited table: a row per saved
/// calculation, its eleven inputs and everything the model produced from them.
/// </summary>
/// <remarks>
/// <para>
/// The culture is a parameter and never a default, because this table is read by a
/// spreadsheet rather than by code. Excel parses a cell with the decimal separator
/// of the machine it runs on, and it splits rows on that same culture's list
/// separator — so a file written invariant lands in a Russian Excel as eleven
/// columns of text, which is worse than useless for an operator who wants to plot
/// it. Both the numbers and the delimiter therefore come from one explicitly
/// chosen culture, and the caller says which.
/// </para>
/// <para>
/// This is the one export that is not invariant, and it is deliberate: see
/// <c>AGENTS.md</c> §1.5. Anything written for a machine to read back still goes
/// through <see cref="CultureInfo.InvariantCulture"/>.
/// </para>
/// </remarks>
public static class HistoryTable
{
    /// <summary>Extension of the file this table is written to.</summary>
    public const string FileExtension = ".csv";

    /// <summary>Renders the saved calculations as delimited text, newest row last.</summary>
    /// <param name="history">The session history, in the order it was saved.</param>
    /// <param name="culture">The culture whose number format and list separator to write in.</param>
    /// <returns>The table, one line per calculation under a single header line.</returns>
    public static string Render(IEnumerable<HistoryEntryViewModel> history, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(culture);

        string separator = culture.TextInfo.ListSeparator;
        StringBuilder text = new();

        AppendLine(text, separator, Header());

        foreach (HistoryEntryViewModel entry in history)
        {
            AppendLine(text, separator, Row(entry, culture));
        }

        return text.ToString();
    }

    private static IEnumerable<string> Header()
    {
        yield return "№";
        yield return "Время";

        foreach (ParameterLabel label in ParameterLabels.All)
        {
            yield return $"{label.ShortName}, {label.Unit}";
        }

        yield return "Тц, °C";
        yield return "Тгр, °C";
        yield return "Тпер, °C";
        yield return "Тц/Тгр";
        yield return "Тпер/Тц";
        yield return "Тпер/Тгр";
        yield return "Область";
    }

    private static IEnumerable<string> Row(HistoryEntryViewModel entry, CultureInfo culture)
    {
        ProfileRatios ratios = ProfileRatios.Of(entry.Profile);

        yield return entry.Number.ToString(culture);
        yield return entry.SavedAt.ToString("HH:mm:ss", culture);

        foreach (ParameterLabel label in ParameterLabels.All)
        {
            ParameterRange range = ParameterCatalog.RangeOf(label.Parameter);
            yield return Number(entry.Parameters[label.Parameter], range.Decimals, culture);
        }

        yield return Number(entry.Profile.Center, DisplayNumber.TemperatureDecimals, culture);
        yield return Number(entry.Profile.OreRidge, DisplayNumber.TemperatureDecimals, culture);
        yield return Number(entry.Profile.Periphery, DisplayNumber.TemperatureDecimals, culture);
        yield return Number(ratios.CenterToOreRidge, DisplayNumber.RatioDecimals, culture);
        yield return Number(ratios.PeripheryToCenter, DisplayNumber.RatioDecimals, culture);
        yield return Number(ratios.PeripheryToOreRidge, DisplayNumber.RatioDecimals, culture);

        // A saved row outlives the screen that flagged it, so it carries its own
        // verdict: without it an extrapolated calculation is indistinguishable
        // from a sound one once it is in a spreadsheet.
        yield return IsExtrapolated(entry.Parameters) ? "экстраполяция" : "в области";
    }

    private static bool IsExtrapolated(ParameterSet parameters) => ParameterCatalog.All.Any(
        parameter => !ParameterCatalog.RangeOf(parameter).Contains(parameters[parameter]));

    private static string Number(double value, int decimals, CultureInfo culture) =>
        value.ToString($"F{decimals}", culture);

    private static void AppendLine(StringBuilder text, string separator, IEnumerable<string> cells)
    {
        text.AppendJoin(separator, cells.Select(cell => Quote(cell, separator)));

        // CRLF, because this file is opened on Windows by a spreadsheet, not by a
        // text tool that would normalise the endings for us.
        text.Append("\r\n");
    }

    /// <summary>Wraps a cell in quotes when the delimiter would otherwise split it.</summary>
    private static string Quote(string cell, string separator) =>
        cell.Contains(separator, StringComparison.Ordinal)
            || cell.Contains('"', StringComparison.Ordinal)
            || cell.Contains('\n', StringComparison.Ordinal)
            ? '"' + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + '"'
            : cell;
}
