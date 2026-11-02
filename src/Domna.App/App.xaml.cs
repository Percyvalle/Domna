using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Domna.Calculation;
using Domna.Domain.Interfaces;
using Domna.UI.ViewModels;

namespace Domna.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // WPF binds using the thread culture unless told otherwise; without this
        // every bound number would render as invariant en-US regardless of the
        // operator's Windows locale.
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

        // The one place an implementation is chosen. Both view-model layers see
        // interfaces only, which is what keeps them testable without a UI thread.
        ITemperatureProfileCalculator calculator = new TemperatureProfileCalculator();
        IParameterOptimizer optimizer = new DifferentialEvolutionOptimizer(calculator);

        MainWindow window = new()
        {
            DataContext = new MainViewModel(
                calculator, optimizer, new HistoryExportFile(), TimeProvider.System),
        };

        window.Show();
    }
}
