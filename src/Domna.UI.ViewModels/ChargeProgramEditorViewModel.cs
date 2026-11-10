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

    /// <summary>
    /// Parses <paramref name="clipboardText"/> (TSV copied from Excel) and updates the rows.
    /// Three formats are accepted:
    /// <list type="bullet">
    /// <item>With type column, single line, and <paramref name="insertAfterIndex"/> ≥ 0:
    /// the parsed row is inserted after the given index and becomes the selected row.</item>
    /// <item>With type column, multiple lines (or no insertion index): all existing rows
    /// are replaced.</item>
    /// <item>Without type column — first cell is a number: existing row types are kept
    /// and their station values are overwritten in order. The number of pasted rows must
    /// not exceed the number of existing rows.</item>
    /// </list>
    /// Sets <see cref="Error"/> and returns <see langword="false"/> on parse failure.
    /// </summary>
    public bool TryPaste(string clipboardText, int insertAfterIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(clipboardText);

        string[] lines = clipboardText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            Error = "Буфер обмена пуст.";
            return false;
        }

        string firstCell = lines[0].Split('\t')[0].Trim().ToUpperInvariant();
        return firstCell is "К" or "K" or "Р" or "P"
            ? TryPasteFullRows(lines, insertAfterIndex)
            : TryPasteStationValues(lines);
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

    // Full-replace (or single-row insert): each line starts with К/Р, then 11 station values.
    // When lines.Length == 1 and insertAfterIndex >= 0, inserts the row instead of replacing all.
    private bool TryPasteFullRows(string[] lines, int insertAfterIndex)
    {
        List<ChargeDropRowViewModel> parsed = new(lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            string[] cells = lines[i].Split('\t');
            ChargeMaterial? material = (cells.Length > 0 ? cells[0].Trim().ToUpperInvariant() : string.Empty) switch
            {
                "К" or "K" => ChargeMaterial.Coke,
                "Р" or "P" => ChargeMaterial.Ore,
                _ => null,
            };

            if (material is null)
            {
                string found = cells.Length > 0 ? cells[0].Trim() : string.Empty;
                Error = string.Create(CultureInfo.CurrentCulture, $"Строка {i + 1}: неизвестный тип '{found}'. Укажите К для кокса или Р для руды.");
                return false;
            }

            if (!TryParseStationCells(cells, startCell: 1, lineNumber: i + 1, out int[]? counts))
            {
                return false;
            }

            parsed.Add(new ChargeDropRowViewModel(material.Value, new ChargeDrop(material.Value, counts)));
        }

        if (lines.Length == 1 && insertAfterIndex >= 0)
        {
            int insertAt = Math.Min(insertAfterIndex + 1, Rows.Count);
            Rows.Insert(insertAt, parsed[0]);
            SelectedRow = parsed[0];
        }
        else
        {
            Rows.Clear();
            foreach (ChargeDropRowViewModel row in parsed)
            {
                Rows.Add(row);
            }
        }

        Error = null;
        return true;
    }

    // Update-in-place: 11 station values per line (descending order), no type column.
    // The material type is taken from the existing rows in the same position.
    private bool TryPasteStationValues(string[] lines)
    {
        if (Rows.Count == 0)
        {
            Error = "Таблица пуста. Вставьте данные вместе с типом материала (К или Р) в первой колонке.";
            return false;
        }

        if (lines.Length > Rows.Count)
        {
            Error = string.Create(
                CultureInfo.CurrentCulture,
                $"Вставляемых строк ({lines.Length}) больше, чем строк в таблице ({Rows.Count}). Добавьте строки заранее или вставьте вместе с типами (К/Р).");
            return false;
        }

        // Parse all lines before modifying rows.
        int[][] allCounts = new int[lines.Length][];
        for (int i = 0; i < lines.Length; i++)
        {
            string[] cells = lines[i].Split('\t');
            if (!TryParseStationCells(cells, startCell: 0, lineNumber: i + 1, out int[]? counts))
            {
                return false;
            }

            allCounts[i] = counts;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            ChargeMaterial material = Rows[i].Material?.Trim().ToUpperInvariant() switch
            {
                "К" => ChargeMaterial.Coke,
                _ => ChargeMaterial.Ore,
            };
            Rows[i] = new ChargeDropRowViewModel(material, new ChargeDrop(material, allCounts[i]));
        }

        Error = null;
        return true;
    }

    // Parses up to 11 station values from cells starting at startCell, in descending station
    // order (cell startCell → station 11, cell startCell+10 → station 1).
    private bool TryParseStationCells(string[] cells, int startCell, int lineNumber, [NotNullWhen(true)] out int[]? counts)
    {
        counts = null;
        int[] result = new int[ChargeDrop.StationCount];
        for (int j = 0; j < ChargeDrop.StationCount; j++)
        {
            int cellIdx = startCell + j;
            if (cellIdx >= cells.Length || string.IsNullOrWhiteSpace(cells[cellIdx]))
            {
                continue;
            }

            string raw = cells[cellIdx].Trim();
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                && !int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out count))
            {
                Error = string.Create(CultureInfo.CurrentCulture, $"Строка {lineNumber}, станция {ChargeDrop.StationCount - j}: неверное значение '{raw}'.");
                return false;
            }

            if (count < 0)
            {
                Error = string.Create(CultureInfo.CurrentCulture, $"Строка {lineNumber}, станция {ChargeDrop.StationCount - j}: число порций не может быть отрицательным.");
                return false;
            }

            result[10 - j] = count;
        }

        counts = result;
        return true;
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
