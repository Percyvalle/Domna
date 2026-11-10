using Domna.Calculation;
using Domna.Domain.Models;
using Domna.Domain.Parameters;

namespace Domna.UI.ViewModels.Tests;

public class MainViewModelTests
{
    private const string Newline = "\r\n";

    private static readonly DateTimeOffset Noon = new(2026, 9, 2, 12, 34, 56, TimeSpan.Zero);

    private readonly StubCalculator _calculator = new();
    private readonly StubOptimizer _optimizer = new();
    private readonly RecordingExportTarget _export = new();
    private readonly MainViewModel _screen;

    public MainViewModelTests() =>
        _screen = new MainViewModel(_calculator, new ChargeProgramCalculator(), _optimizer, _export, new FixedTimeProvider(Noon));

    [Fact]
    public void BeforeTheFirstCalculation_ThereIsNoResultToRead()
    {
        Assert.False(_screen.HasResult);
        Assert.Null(_screen.Profile);
        Assert.All(_screen.Zones, zone => Assert.Null(zone.Celsius));
        Assert.All(_screen.Ratios, ratio => Assert.Null(ratio.Value));
        Assert.Contains("не запускался", _screen.StatusLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Calculate_ProducesTheReferenceProfileAtTheDefaults()
    {
        _screen.Calculate();

        Assert.True(_screen.HasResult);
        Assert.Equal(272.59362591618179, _screen.Zones[0].Celsius!.Value, 1e-9);
        Assert.Equal(139.41992678663084, _screen.Zones[1].Celsius!.Value, 1e-9);
        Assert.Equal(170.40870586815851, _screen.Zones[2].Celsius!.Value, 1e-9);
        Assert.False(_screen.IsStale);
    }

    [Fact]
    public void EditingAnInput_MarksTheShownResultStaleWithoutReplacingIt()
    {
        _screen.Calculate();
        double before = _screen.Zones[0].Celsius!.Value;

        _screen.Inputs[0].Value = 2.4;

        Assert.True(_screen.IsStale);
        Assert.Equal(before, _screen.Zones[0].Celsius!.Value);
        Assert.Contains("устарел", _screen.StatusLine, StringComparison.Ordinal);
    }

    [Fact]
    public void AStaleResultCannotBePrinted()
    {
        _screen.Calculate();
        _screen.Inputs[0].Value = 2.4;

        Assert.False(_screen.OpenProtocolCommand.CanExecute(null));
    }

    [Fact]
    public void Calculate_SavesResultToHistory()
    {
        _screen.Inputs[(int)Parameter.BurdenLevel].Value = 2.4;
        _screen.Calculate();

        HistoryEntryViewModel entry = Assert.Single(_screen.History);
        Assert.Equal(2.4, entry.Parameters[Parameter.BurdenLevel]);
        Assert.Equal(_screen.Profile, entry.Profile);
        Assert.False(_screen.IsStale);
    }

    [Fact]
    public void Calculate_SavesNothingWhenTheCalculationFails()
    {
        _calculator.Override = new TemperatureProfile(double.NaN, 1.0, 1.0);

        _screen.Calculate();

        Assert.Empty(_screen.History);
        Assert.True(_screen.HasCalculationError);
    }

    [Fact]
    public void Calculate_MakesTheFirstResultTheBaseline()
    {
        _screen.Calculate();

        HistoryEntryViewModel entry = Assert.Single(_screen.History);
        Assert.True(entry.IsBaseline);
        Assert.Equal("Snapshot #001 | 12:34:56", entry.Label);
        Assert.Equal(_screen.Profile, _screen.BaselineProfile);

        // The baseline is the calculation on screen, so every delta reads as unchanged.
        Assert.All(_screen.Zones, zone => Assert.Equal(DeltaDirection.Unchanged, zone.Delta.Direction));
    }

    [Fact]
    public void ComparingAgainstASavedCalculation_ShowsTheDirectionOfEveryDelta()
    {
        _screen.Calculate();

        _screen.Inputs[(int)Parameter.BurdenLevel].Value = 2.4;
        _screen.Calculate();

        Assert.NotEqual(_screen.Profile, _screen.BaselineProfile);
        Assert.All(_screen.Zones, zone => Assert.NotEqual(DeltaDirection.Unavailable, zone.Delta.Direction));
    }

    [Fact]
    public void DeletingAnEntry_LeavesTheRestOfTheHistoryAlone()
    {
        _screen.Calculate();

        _screen.Inputs[(int)Parameter.BurdenLevel].Value = 2.4;
        _screen.Calculate();

        HistoryEntryViewModel baseline = _screen.History[0];
        _screen.History[1].Delete();

        Assert.Same(baseline, Assert.Single(_screen.History));
        Assert.True(baseline.IsBaseline);
        Assert.Equal(baseline.Profile, _screen.BaselineProfile);
    }

    [Fact]
    public void DeletingTheBaseline_LeavesTheScreenWithoutOne()
    {
        _screen.Calculate();

        _screen.Inputs[(int)Parameter.BurdenLevel].Value = 2.4;
        _screen.Calculate();

        _screen.History[0].Delete();

        // No entry is promoted in its place: which calculation the deltas are read
        // against is the operator's choice, not something to guess at.
        Assert.Single(_screen.History);
        Assert.Null(_screen.BaselineProfile);
        Assert.All(_screen.Zones, zone => Assert.Equal(DeltaDirection.Unavailable, zone.Delta.Direction));
        Assert.False(_screen.Optimization.RunCommand.CanExecute(null));
    }

    [Fact]
    public void DeletingTheLastEntry_EmptiesTheHistory()
    {
        _screen.Calculate();

        _screen.History[0].Delete();

        Assert.Empty(_screen.History);
        Assert.True(_screen.IsHistoryEmpty);
    }

    [Fact]
    public void ExportHistory_IsUnavailableWithNothingToExport()
    {
        Assert.False(_screen.ExportHistoryCommand.CanExecute(null));

        _screen.Calculate();

        Assert.True(_screen.ExportHistoryCommand.CanExecute(null));
    }

    [Fact]
    public void ExportHistory_OffersADatedFileNameAndReportsWhereItLanded()
    {
        _screen.Calculate();
        _screen.ExportHistory();

        Assert.Equal("Домна · история 2026-09-02 12-34.csv", _export.SuggestedFileName);
        Assert.Equal(@"C:\export\Домна · история 2026-09-02 12-34.csv", _screen.LastExportPath);
    }

    [Fact]
    public void ExportHistory_HandsOverEverySavedCalculation()
    {
        _screen.Calculate();
        _screen.Inputs[(int)Parameter.BurdenLevel].Value = 2.4;
        _screen.Calculate();

        _screen.ExportHistory();

        // Header plus one line per saved calculation.
        Assert.Equal(3, _export.Content!.Split(Newline, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void ExportHistory_ReportsNothingWhenTheOperatorCancels()
    {
        _export.Accepts = false;

        _screen.Calculate();
        _screen.ExportHistory();

        Assert.Equal(1, _export.Calls);
        Assert.Null(_screen.LastExportPath);
    }

    [Fact]
    public void ChangingTheHistory_DropsTheExportConfirmation()
    {
        // The file on disk no longer describes the history, so saying where it went
        // would point the operator at a table that is missing a row.
        _screen.Calculate();
        _screen.ExportHistory();

        _screen.Calculate();

        Assert.Null(_screen.LastExportPath);
    }

    [Fact]
    public void ClearHistory_TakesTheBaselineWithIt()
    {
        _screen.Calculate();
        _screen.ClearHistory();

        Assert.Empty(_screen.History);
        Assert.True(_screen.IsHistoryEmpty);
        Assert.Null(_screen.BaselineProfile);
        Assert.All(_screen.Zones, zone => Assert.Equal(DeltaDirection.Unavailable, zone.Delta.Direction));
    }

    [Fact]
    public void LoadingASavedCalculation_PutsItsParametersBackAndRecalculates()
    {
        _screen.Calculate();

        _screen.Inputs[(int)Parameter.BurdenLevel].Value = 2.4;
        _screen.Calculate();
        _screen.History[0].Load();

        Assert.Equal(ParameterCatalog.RangeOf(Parameter.BurdenLevel).Default, _screen.Inputs[0].Value);
        Assert.False(_screen.IsStale);
        Assert.Equal(_screen.BaselineProfile, _screen.Profile);
    }

    [Fact]
    public void Reset_ReturnsEveryParameterToItsCatalogueDefault()
    {
        _screen.Inputs[0].Value = 2.4;
        _screen.Inputs[1].Value = 4.5;

        _screen.Reset();

        Assert.All(_screen.Inputs, input => Assert.Equal(input.Range.Default, input.Value));
        Assert.False(_screen.IsStale);
    }

    [Fact]
    public void AnInputOutsideTheFittedRegion_IsFlaggedButStillComputed()
    {
        _screen.Inputs[(int)Parameter.BurdenLevel].Value = 9.0;
        _screen.Calculate();

        Assert.True(_screen.HasOutOfRangeInputs);
        Assert.True(_screen.Inputs[0].IsOutOfRange);
        Assert.True(_screen.HasResult);
    }

    [Fact]
    public void ANonFiniteResult_KeepsThePreviousOneOnScreenAndSaysSo()
    {
        _screen.Calculate();
        double previous = _screen.Zones[0].Celsius!.Value;

        _calculator.Override = new TemperatureProfile(double.NaN, 1.0, 1.0);
        _screen.Inputs[0].Value = 2.4;
        _screen.Calculate();

        Assert.True(_screen.HasCalculationError);
        Assert.Equal(previous, _screen.Zones[0].Celsius!.Value);
        Assert.True(_screen.IsStale);
    }

    [Fact]
    public void RestoreLastInput_UndoesTheEditThatBrokeTheCalculation()
    {
        _screen.Calculate();
        _calculator.Override = new TemperatureProfile(double.NaN, 1.0, 1.0);
        _screen.Inputs[0].Value = 2.4;
        _screen.Calculate();

        _calculator.Override = null;
        _screen.RestoreLastInput();

        Assert.False(_screen.HasCalculationError);
        Assert.Equal(ParameterCatalog.RangeOf(Parameter.BurdenLevel).Default, _screen.Inputs[0].Value);
        Assert.False(_screen.IsStale);
    }

    [Fact]
    public void FreeParametersCaption_CountsWhatTheOptimizerMayMove()
    {
        Assert.Equal("свободных: 0 / 11", _screen.FreeParametersCaption);

        _screen.Inputs[0].IsFree = true;
        _screen.Inputs[3].IsFree = true;

        Assert.Equal("свободных: 2 / 11", _screen.FreeParametersCaption);
    }

    [Fact]
    public void OpenProtocol_FreezesTheCalculationOnScreen()
    {
        _screen.Calculate();
        _screen.OpenProtocol();

        Assert.True(_screen.IsProtocolOpen);
        Assert.Equal(ParameterCatalog.Count, _screen.Protocol!.Rows.Count);
        Assert.Equal(3, _screen.Protocol.Readings.Count);
        Assert.Contains("02.09.2026", _screen.Protocol.Stamp, StringComparison.Ordinal);
        Assert.DoesNotContain("экстраполяция", _screen.Protocol.RegionCaption, StringComparison.Ordinal);

        _screen.CloseProtocol();
        Assert.False(_screen.IsProtocolOpen);
    }

    [Fact]
    public void TheProtocolSaysWhenAResultIsAnExtrapolation()
    {
        _screen.Inputs[(int)Parameter.BurdenLevel].Value = 9.0;
        _screen.Calculate();
        _screen.OpenProtocol();

        Assert.Contains("экстраполяция", _screen.Protocol!.RegionCaption, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOptimizerIsOfferedOnlyTheParametersTheOperatorReleased()
    {
        _screen.Calculate();
        _screen.Inputs[(int)Parameter.CokeSize].IsFree = true;

        _screen.Optimization.Run();

        // The search runs off the calling thread so the screen stays readable; the
        // test waits for it the same way the screen does — by watching for the result.
        Assert.True(
            SpinWait.SpinUntil(() => _optimizer.LastRequest is not null, TimeSpan.FromSeconds(5)),
            "the optimizer was never called");

        Assert.Equal([Parameter.CokeSize], _optimizer.LastRequest!.FreeParameters);
        Assert.Equal(_screen.BaselineProfile, _optimizer.LastRequest.Target);
    }

    [Fact]
    public void TheOptimizerCannotRunWithoutABaseline()
    {
        _screen.Inputs[0].IsFree = true;

        Assert.False(_screen.Optimization.RunCommand.CanExecute(null));

        _screen.Calculate();

        Assert.True(_screen.Optimization.RunCommand.CanExecute(null));
    }
}
