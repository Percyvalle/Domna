using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Domna.Domain.Models;
using Domna.UI.ViewModels;

namespace Domna.App.Views;

public partial class ChargeProgramWindow : Window
{
    private readonly ChargeProgramEditorViewModel _editor;
    private bool _isCellEditing;

    public ChargeProgramWindow(ChargeProgramEditorViewModel editor)
    {
        InitializeComponent();
        _editor = editor;
        DataContext = editor;
        editor.Rows.CollectionChanged += OnRowsChanged;

        DataGridTextColumn typeColumn = Column("Тип", nameof(ChargeDropRowViewModel.Material));
        typeColumn.IsReadOnly = true;
        Matrix.Columns.Add(typeColumn);
        for (int station = ChargeDrop.StationCount; station >= 1; station--)
        {
            Matrix.Columns.Add(Column(
                station.ToString(CultureInfo.CurrentCulture),
                string.Create(CultureInfo.InvariantCulture, $"{nameof(ChargeDropRowViewModel.Portions)}[{station - 1}]")));
        }

        Matrix.BeginningEdit += (_, _) => _isCellEditing = true;
        Matrix.CellEditEnding += (_, _) => _isCellEditing = false;
    }

    // Intercepts Ctrl+V at the window level so the user can paste a full matrix
    // copied from Excel. Tab-delimited clipboard text is treated as a matrix paste;
    // plain text without tabs passes through to the focused cell editor as usual.
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key == Key.V
            && (Keyboard.Modifiers & ModifierKeys.Control) != 0
            && !_isCellEditing
            && Clipboard.ContainsText()
            && Clipboard.GetText() is { } text
            && text.Contains('\t'))
        {
            _editor.TryPaste(text);
            e.Handled = true;
        }
    }

    private DataGridTextColumn Column(string header, string path) => new()
    {
        Header = header,
        Binding = new Binding(path) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
        ElementStyle = (Style)FindResource("ChargeProgram.CellText"),
        EditingElementStyle = (Style)FindResource("ChargeProgram.CellEditor"),
    };

    private void ScrollToSelectedRow(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == Matrix && Matrix.SelectedItem is { } row)
        {
            Matrix.ScrollIntoView(row);
        }
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move && e.NewItems?[0] is { } row)
        {
            _ = Dispatcher.InvokeAsync(() => Matrix.ScrollIntoView(row), DispatcherPriority.Loaded);
        }
    }

    private void ApplyProgram(object sender, RoutedEventArgs e)
    {
        if (Matrix.CommitEdit(DataGridEditingUnit.Row, true) && _editor.TryApply())
        {
            DialogResult = true;
        }
    }
}
