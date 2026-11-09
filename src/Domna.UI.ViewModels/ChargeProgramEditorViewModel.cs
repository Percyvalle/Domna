using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domna.Domain.Models;

namespace Domna.UI.ViewModels;

public sealed partial class ChargeProgramEditorViewModel : ObservableObject
{
    private readonly Func<ChargeProgram, bool> _apply;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveRowCommand))]
    private ChargeDropRowViewModel? _selectedRow;

    internal ChargeProgramEditorViewModel(ChargeProgram? program, Func<ChargeProgram, bool> apply)
    {
        _apply = apply;
        Rows = program is null
            ? [new(ChargeMaterial.Coke), new(ChargeMaterial.Ore)]
            : [.. program.Drops.Select(drop => new ChargeDropRowViewModel(drop.Material, drop))];
        Rows.CollectionChanged += (_, _) =>
        {
            Error = null;
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
            RemoveRowCommand.NotifyCanExecuteChanged();
        };
    }

    public ObservableCollection<ChargeDropRowViewModel> Rows { get; }

    private int SelectedIndex => SelectedRow is { } row ? Rows.IndexOf(row) : -1;

    public bool TryApply()
    {
        List<ChargeDrop> drops = new(Rows.Count);
        for (int row = 0; row < Rows.Count; row++)
        {
            if (!TryReadDrop(Rows[row], row + 1, out ChargeDrop? drop))
            {
                return false;
            }

            drops.Add(drop);
        }

        if (!_apply(new ChargeProgram(drops)))
        {
            Error = "Укажите порции кокса и руды. В каждой группе 9–11, 6–8 и 3–5 нужна хотя бы одна порция.";
            return false;
        }

        Error = null;
        return true;
    }

    [RelayCommand]
    private void AddCoke() => AddRow(ChargeMaterial.Coke);

    [RelayCommand]
    private void AddOre() => AddRow(ChargeMaterial.Ore);

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => MoveRow(-1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => MoveRow(1);

    [RelayCommand(CanExecute = nameof(CanRemoveRow))]
    private void RemoveRow()
    {
        int index = SelectedIndex;
        if (index < 0)
        {
            return;
        }

        Rows.RemoveAt(index);
        SelectedRow = Rows.Count == 0 ? null : Rows[Math.Min(index, Rows.Count - 1)];
    }

    private bool CanMoveUp() => SelectedIndex > 0;

    private bool CanMoveDown() => SelectedIndex >= 0 && SelectedIndex < Rows.Count - 1;

    private bool CanRemoveRow() => SelectedIndex >= 0;

    private void AddRow(ChargeMaterial material)
    {
        ChargeDropRowViewModel row = new(material);
        int index = SelectedIndex;
        Rows.Insert(index < 0 ? Rows.Count : index + 1, row);
        SelectedRow = row;
    }

    private void MoveRow(int offset)
    {
        int from = SelectedIndex;
        int to = from + offset;
        if (from >= 0 && to >= 0 && to < Rows.Count)
        {
            ChargeDropRowViewModel row = Rows[from];
            Rows.Move(from, to);
            SelectedRow = row;
        }
    }

    private bool TryReadDrop(
        ChargeDropRowViewModel row,
        int rowNumber,
        [NotNullWhen(true)] out ChargeDrop? drop)
    {
        drop = null;
        ChargeMaterial? material = row.Material?.Trim().ToUpperInvariant() switch
        {
            "К" => ChargeMaterial.Coke,
            "Р" => ChargeMaterial.Ore,
            _ => null,
        };
        if (material is null)
        {
            Error = string.Create(CultureInfo.CurrentCulture, $"Строка {rowNumber}. Укажите К для кокса или Р для руды.");
            return false;
        }

        int[] counts = new int[ChargeDrop.StationCount];
        for (int index = 0; index < counts.Length; index++)
        {
            string text = row.Portions[index];
            if (!string.IsNullOrWhiteSpace(text)
                && (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out counts[index]) || counts[index] < 0))
            {
                Error = string.Create(
                    CultureInfo.CurrentCulture,
                    $"Строка {rowNumber}, станция {index + 1}. Введите целое число от нуля.");
                return false;
            }
        }

        drop = new ChargeDrop(material.Value, counts);
        return true;
    }
}
