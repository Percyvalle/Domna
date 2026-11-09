using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Domna.Domain.Models;
using Domna.UI.ViewModels;

namespace Domna.App.Views;

public partial class ChargeProgramWindow : Window
{
    private readonly ChargeProgramEditorViewModel _editor;

    public ChargeProgramWindow(ChargeProgramEditorViewModel editor)
    {
        InitializeComponent();
        _editor = editor;
        DataContext = editor;
        editor.Rows.CollectionChanged += OnRowsChanged;

        Matrix.Columns.Add(Column("Тип", nameof(ChargeDropRowViewModel.Material)));
        for (int station = ChargeDrop.StationCount; station >= 1; station--)
        {
            Matrix.Columns.Add(Column(
                station.ToString(CultureInfo.CurrentCulture),
                string.Create(CultureInfo.InvariantCulture, $"{nameof(ChargeDropRowViewModel.Portions)}[{station - 1}]")));
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
