using System.IO;
using System.Text;
using Domna.UI.ViewModels;
using Microsoft.Win32;

namespace Domna.App;

/// <summary>
/// Writes an exported history table to a file the operator picks.
/// </summary>
/// <remarks>
/// The bytes are UTF-8 <em>with</em> a byte-order mark. Excel reads a CSV without
/// one in the machine's ANSI code page, which turns every Cyrillic heading into
/// mojibake; the BOM is what makes it read UTF-8 instead.
/// </remarks>
internal sealed class HistoryExportFile : IHistoryExportTarget
{
    private static readonly UTF8Encoding ExcelReadableUtf8 = new(encoderShouldEmitUTF8Identifier: true);

    public string? Write(string suggestedFileName, string content)
    {
        SaveFileDialog dialog = new()
        {
            Title = "Сохранить историю расчётов",
            FileName = suggestedFileName,
            DefaultExt = HistoryTable.FileExtension,
            Filter = "Таблица CSV для Excel (*.csv)|*.csv|Все файлы (*.*)|*.*",
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        File.WriteAllText(dialog.FileName, content, ExcelReadableUtf8);

        return dialog.FileName;
    }
}
