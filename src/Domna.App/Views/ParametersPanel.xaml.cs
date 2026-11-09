using System.Windows;
using System.Windows.Controls;
using Domna.UI.ViewModels;

namespace Domna.App.Views;

public partial class ParametersPanel : UserControl
{
    public ParametersPanel() => InitializeComponent();

    private void OpenChargeProgram(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel screen)
        {
            ChargeProgramWindow window = new(screen.EditChargeProgram()) { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        }
    }
}
