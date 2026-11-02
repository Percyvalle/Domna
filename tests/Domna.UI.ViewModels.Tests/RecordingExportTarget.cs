namespace Domna.UI.ViewModels.Tests;

/// <summary>An export target that keeps what it was handed instead of touching disk.</summary>
internal sealed class RecordingExportTarget : IHistoryExportTarget
{
    /// <summary>Set to <see langword="false"/> to act as an operator who cancelled the dialog.</summary>
    public bool Accepts { get; set; } = true;

    public string? SuggestedFileName { get; private set; }

    public string? Content { get; private set; }

    public int Calls { get; private set; }

    public string? Write(string suggestedFileName, string content)
    {
        Calls++;
        SuggestedFileName = suggestedFileName;
        Content = content;

        return Accepts ? @"C:\export\" + suggestedFileName : null;
    }
}
