using Domna.Calculation;
using Domna.Domain.Parameters;

namespace Domna.UI.ViewModels.Tests;

public class ChargeProgramEditorTests
{
    private readonly MainViewModel _screen = new(
        new StubCalculator(),
        new ChargeProgramCalculator(),
        new StubOptimizer(),
        new RecordingExportTarget(),
        new FixedTimeProvider(new DateTimeOffset(2026, 9, 2, 12, 34, 56, TimeSpan.Zero)));

    [Fact]
    public void ApplyingEditedMatrixUpdatesStationInputsAndSnapshotRestoresItsMatrix()
    {
        ChargeProgramEditorViewModel editor = _screen.EditChargeProgram();
        SetPortions(editor.Rows[0], 3, 6, 9);
        SetPortions(editor.Rows[1], 3, 6, 9);

        Assert.True(editor.TryApply(), editor.Error);
        _screen.Calculate();
        Assert.Equal(50, Value(Parameter.ChargeStations9To11), 12);
        _screen.Inputs[(int)Parameter.ChargeStations9To11].IsFree = true;
        Assert.False(_screen.Optimization.RunCommand.CanExecute(null));
        _screen.Inputs[(int)Parameter.BurdenLevel].IsFree = true;
        Assert.True(_screen.Optimization.RunCommand.CanExecute(null));

        editor = _screen.EditChargeProgram();
        editor.Rows[1].Portions[5] = string.Empty;
        editor.Rows[1].Portions[8] = "2";

        Assert.True(editor.TryApply(), editor.Error);
        Assert.Equal(200.0 / 3, Value(Parameter.ChargeStations9To11), 12);
        Assert.Equal(0, Value(Parameter.ChargeStations6To8), 12);
        Assert.Equal(50, Value(Parameter.ChargeStations3To5), 12);
        Assert.True(_screen.IsStale);
        Assert.False(_screen.Optimization.RunCommand.CanExecute(null));

        _screen.History[0].Load();

        Assert.Equal(50, Value(Parameter.ChargeStations9To11), 12);
        Assert.Equal("1", _screen.EditChargeProgram().Rows[1].Portions[5]);
        Assert.True(_screen.Optimization.RunCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyingMatrixLeavesTheOtherEightInputsEditableAndUnchanged()
    {
        Parameter[] stationParameters =
        [
            Parameter.ChargeStations9To11,
            Parameter.ChargeStations6To8,
            Parameter.ChargeStations3To5,
        ];
        ParameterInputViewModel[] otherInputs = [.. _screen.Inputs.Where(input => !stationParameters.Contains(input.Parameter))];
        foreach (ParameterInputViewModel input in otherInputs)
        {
            input.Value = input.Range.Minimum;
        }

        ChargeProgramEditorViewModel editor = _screen.EditChargeProgram();
        SetPortions(editor.Rows[0], 3, 6, 9);
        SetPortions(editor.Rows[1], 3, 6, 9);

        Assert.True(editor.TryApply(), editor.Error);
        Assert.Equal(stationParameters, _screen.Inputs.Where(input => input.IsCalculated).Select(input => input.Parameter));
        Assert.All(otherInputs, input =>
        {
            Assert.Equal(input.Range.Minimum, input.Value);
            Assert.True(input.CanOptimize);
        });
    }

    [Fact]
    public void AddingAndMovingMaterialRowsPreservesTheirPortionsWhenReopening()
    {
        ChargeProgramEditorViewModel editor = _screen.EditChargeProgram();
        editor.Rows.Clear();

        editor.AddCokeCommand.Execute(null);
        SetPortions(editor.Rows[0], 3, 6, 9);
        editor.AddOreCommand.Execute(null);
        SetPortions(editor.Rows[1], 4, 7, 10);

        Assert.Same(editor.Rows[1], editor.SelectedRow);
        Assert.False(editor.MoveDownCommand.CanExecute(null));
        editor.MoveUpCommand.Execute(null);
        Assert.Same(editor.Rows[0], editor.SelectedRow);
        Assert.False(editor.MoveUpCommand.CanExecute(null));
        Assert.True(editor.MoveDownCommand.CanExecute(null));

        Assert.True(editor.TryApply(), editor.Error);
        ChargeProgramEditorViewModel reopened = _screen.EditChargeProgram();

        Assert.Collection(
            reopened.Rows,
            ore =>
            {
                Assert.Equal("Р", ore.Material);
                Assert.Equal("1", ore.Portions[9]);
            },
            coke =>
            {
                Assert.Equal("К", coke.Material);
                Assert.Equal("1", coke.Portions[8]);
            });
    }

    [Fact]
    public void ApplyingInvalidCellLeavesTheLastInputsInPlace()
    {
        ChargeProgramEditorViewModel editor = _screen.EditChargeProgram();
        SetPortions(editor.Rows[0], 3, 6, 9);
        SetPortions(editor.Rows[1], 3, 6, 9);
        editor.Rows[0].Portions[2] = "-1";

        Assert.False(editor.TryApply());
        Assert.Contains("станция 3", editor.Error, StringComparison.Ordinal);
        Assert.Equal(75, Value(Parameter.ChargeStations9To11), 12);
    }

    [Fact]
    public void TryPaste_WithTypeColumn_ReplacesAllRows()
    {
        ChargeProgramEditorViewModel editor = _screen.EditChargeProgram();

        // Two rows: Coke with station 9 = 2, Ore with station 6 = 1.
        // Columns in descending order: 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1
        bool ok = editor.TryPaste("К\t0\t0\t2\t0\t0\t0\t0\t0\t0\t0\t0\r\nР\t0\t0\t0\t0\t0\t1\t0\t0\t0\t0\t0\r\n");

        Assert.True(ok, editor.Error);
        Assert.Equal(2, editor.Rows.Count);
        Assert.Equal("К", editor.Rows[0].Material);
        Assert.Equal("2", editor.Rows[0].Portions[8]); // station 9 → index 8
        Assert.Equal("Р", editor.Rows[1].Material);
        Assert.Equal("1", editor.Rows[1].Portions[5]); // station 6 → index 5
    }

    [Fact]
    public void TryPaste_WithoutTypeColumn_OverwritesPortionsKeepingExistingTypes()
    {
        ChargeProgramEditorViewModel editor = _screen.EditChargeProgram();

        // Start with one Coke row and one Ore row (default editor).
        // Paste only numbers: station 9 = 3 for first row, station 6 = 2 for second row.
        bool ok = editor.TryPaste("0\t0\t3\t0\t0\t0\t0\t0\t0\t0\t0\r\n0\t0\t0\t0\t0\t2\t0\t0\t0\t0\t0\r\n");

        Assert.True(ok, editor.Error);
        Assert.Equal(2, editor.Rows.Count);
        Assert.Equal("К", editor.Rows[0].Material); // type preserved
        Assert.Equal("3", editor.Rows[0].Portions[8]); // station 9
        Assert.Equal("Р", editor.Rows[1].Material); // type preserved
        Assert.Equal("2", editor.Rows[1].Portions[5]); // station 6
    }

    [Fact]
    public void TryPaste_WithoutTypeColumn_TooManyLines_ReturnsError()
    {
        ChargeProgramEditorViewModel editor = _screen.EditChargeProgram();

        // 3 pasted rows but only 2 existing rows.
        bool ok = editor.TryPaste(
            "0\t0\t1\t0\t0\t0\t0\t0\t0\t0\t0\r\n" +
            "0\t0\t0\t0\t0\t1\t0\t0\t0\t0\t0\r\n" +
            "0\t1\t0\t0\t0\t0\t0\t0\t0\t0\t0\r\n");

        Assert.False(ok);
        Assert.NotNull(editor.Error);
    }

    [Fact]
    public void TryPaste_InvalidValue_SetsError()
    {
        ChargeProgramEditorViewModel editor = _screen.EditChargeProgram();

        bool ok = editor.TryPaste("К\t0\t0\txyz\t0\t0\t0\t0\t0\t0\t0\t0\r\n");

        Assert.False(ok);
        Assert.Contains("станция 9", editor.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryPaste_LatinKP_AcceptedAsMaterial()
    {
        ChargeProgramEditorViewModel editor = _screen.EditChargeProgram();

        bool ok = editor.TryPaste("K\t0\t0\t1\t0\t0\t0\t0\t0\t0\t0\t0\r\nP\t0\t0\t0\t0\t1\t0\t0\t0\t0\t0\t0\r\n");

        Assert.True(ok, editor.Error);
        Assert.Equal("К", editor.Rows[0].Material);
        Assert.Equal("Р", editor.Rows[1].Material);
    }

    private static void SetPortions(ChargeDropRowViewModel row, params int[] stations)
    {
        foreach (int station in stations)
        {
            row.Portions[station - 1] = "1";
        }
    }

    private double Value(Parameter parameter) => _screen.Inputs[(int)parameter].Value;
}
