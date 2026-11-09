using System.Globalization;
using Domna.Calculation;
using Domna.Domain.Parameters;

namespace Domna.UI.ViewModels.Tests;

public class HistoryTableTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 2, 12, 34, 56, TimeSpan.Zero);
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly CultureInfo American = CultureInfo.GetCultureInfo("en-US");

    private readonly MainViewModel _screen =
        new(new StubCalculator(), new ChargeProgramCalculator(), new StubOptimizer(), new RecordingExportTarget(), new FixedTimeProvider(Noon));

    [Fact]
    public void AnEmptyHistory_StillCarriesItsHeader()
    {
        string[] lines = Lines(HistoryTable.Render(_screen.History, Russian));

        Assert.Single(lines);
        Assert.StartsWith("№;Время;", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void EveryColumnIsNamed_OnceForEachValueInARow()
    {
        _screen.SaveSnapshot();

        string[] lines = Lines(HistoryTable.Render(_screen.History, Russian));

        // № + time + eleven parameters + three temperatures + three ratios + the verdict.
        Assert.Equal(20, lines[0].Split(';').Length);
        Assert.Equal(lines[0].Split(';').Length, lines[1].Split(';').Length);
    }

    [Fact]
    public void ARussianTableUsesTheCommaAndSemicolonExcelExpectsThere()
    {
        _screen.SaveSnapshot();

        string row = Lines(HistoryTable.Render(_screen.History, Russian))[1];

        Assert.StartsWith("1;12:34:56;", row, StringComparison.Ordinal);
        Assert.Contains("272,6", row, StringComparison.Ordinal);
        Assert.DoesNotContain("272.6", row, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAmericanTableUsesThePointAndComma()
    {
        _screen.SaveSnapshot();

        string row = Lines(HistoryTable.Render(_screen.History, American))[1];

        Assert.StartsWith("1,12:34:56,", row, StringComparison.Ordinal);
        Assert.Contains("272.6", row, StringComparison.Ordinal);
    }

    [Fact]
    public void ACellHoldingTheSeparatorIsQuoted()
    {
        _screen.SaveSnapshot();

        // A Russian header names units after a comma; under en-US the comma is the
        // delimiter, so the heading has to be quoted or the columns shift by one.
        string header = Lines(HistoryTable.Render(_screen.History, American))[0];

        Assert.Contains("\"Уровень засыпи, м\"", header, StringComparison.Ordinal);
    }

    [Fact]
    public void EachSavedCalculationBecomesOneRow_InTheOrderItWasSaved()
    {
        _screen.SaveSnapshot();
        _screen.Inputs[(int)Parameter.BurdenLevel].Value = 2.4;
        _screen.SaveSnapshot();

        string[] lines = Lines(HistoryTable.Render(_screen.History, Russian));

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("1;", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("2;", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void ARowSaysWhetherItLeftTheFittedRegion()
    {
        _screen.SaveSnapshot();

        ParameterRange range = ParameterCatalog.RangeOf(Parameter.BurdenLevel);
        _screen.Inputs[(int)Parameter.BurdenLevel].Value = range.Maximum + 1.0;
        _screen.SaveSnapshot();

        string[] lines = Lines(HistoryTable.Render(_screen.History, Russian));

        Assert.EndsWith(";в области", lines[1], StringComparison.Ordinal);
        Assert.EndsWith(";экстраполяция", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void RowsEndWithTheLineBreakAWindowsSpreadsheetExpects()
    {
        _screen.SaveSnapshot();

        Assert.EndsWith("\r\n", HistoryTable.Render(_screen.History, Russian), StringComparison.Ordinal);
    }

    private static string[] Lines(string table) =>
        table.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
}
