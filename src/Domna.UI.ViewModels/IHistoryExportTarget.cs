namespace Domna.UI.ViewModels;

/// <summary>
/// Where an exported history table is written — asks the operator for a location
/// and puts the text there.
/// </summary>
/// <remarks>
/// A port, not a service: choosing a file is a desktop gesture, so the view model
/// states what it needs and <c>Domna.App</c> supplies the file dialog. This keeps
/// the export testable without a UI thread.
/// </remarks>
public interface IHistoryExportTarget
{
    /// <summary>Asks where to save and writes <paramref name="content"/> there.</summary>
    /// <param name="suggestedFileName">File name to offer, extension included.</param>
    /// <param name="content">The table to write.</param>
    /// <returns>The path written, or <see langword="null"/> if the operator cancelled.</returns>
    string? Write(string suggestedFileName, string content);
}
