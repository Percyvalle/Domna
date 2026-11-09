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
        _screen.SaveSnapshot();
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

    private static void SetPortions(ChargeDropRowViewModel row, params int[] stations)
    {
        foreach (int station in stations)
        {
            row.Portions[station - 1] = "1";
        }
    }

    private double Value(Parameter parameter) => _screen.Inputs[(int)parameter].Value;
}
