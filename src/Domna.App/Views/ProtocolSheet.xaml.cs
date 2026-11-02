using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Xps;

namespace Domna.App.Views;

public partial class ProtocolSheet : UserControl
{
    /// <summary>The Windows PDF writer, present on every supported Windows version.</summary>
    private const string PdfPrinterName = "Microsoft Print to PDF";

    /// <summary>A4 at 96 dpi, the page the PDF is laid out on.</summary>
    private const double PageWidth = 793.7;
    private const double PageHeight = 1122.5;

    private const double PageMargin = 48.0;

    private const string DocumentName = "Протокол расчёта колошника";

    public ProtocolSheet() => InitializeComponent();

    /// <summary>
    /// Sends the protocol to a printer the operator picks.
    /// </summary>
    /// <remarks>
    /// Laying a visual out on a page is a view operation, so it lives in the view.
    /// What the document says was already fixed by <c>ProtocolViewModel</c> when the
    /// protocol was opened; nothing here decides any of it.
    /// </remarks>
    private void OnPrint(object sender, RoutedEventArgs e)
    {
        PrintDialog dialog = new();
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        dialog.PrintVisual(Page(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight), DocumentName);
    }

    /// <summary>
    /// Writes the protocol to a PDF file.
    /// </summary>
    /// <remarks>
    /// Straight to the Windows PDF writer, with no print dialog in the way: the
    /// operator asked to save a file, so the only question worth asking is where.
    /// That question is the PDF writer's own save dialog.
    /// </remarks>
    private void OnSaveAsPdf(object sender, RoutedEventArgs e)
    {
        PrintQueue queue;
        try
        {
            using LocalPrintServer server = new();
            queue = server.GetPrintQueue(PdfPrinterName);
        }
        catch (PrintQueueException)
        {
            MessageBox.Show(
                $"Принтер «{PdfPrinterName}» не установлен, сохранить PDF нечем. "
                + "Его можно включить в «Программы и компоненты» → «Включение или отключение компонентов Windows», "
                + "либо распечатать протокол через «Печать».",
                "Сохранение PDF",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        PrintTicket ticket = queue.DefaultPrintTicket?.Clone() ?? new PrintTicket();
        ticket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
        ticket.PageOrientation = PageOrientation.Portrait;

        try
        {
            XpsDocumentWriter writer = PrintQueue.CreateXpsDocumentWriter(queue);
            writer.Write(Page(PageWidth, PageHeight), ticket);
        }
        catch (PrintingCanceledException)
        {
            // The operator closed the save dialog. Nothing was written and nothing
            // is wrong, so the screen says nothing either.
        }
    }

    /// <summary>The protocol laid out on one page of the given size.</summary>
    /// <remarks>
    /// A fresh copy of the document rather than the one on screen: the on-screen
    /// copy sits inside a scroll viewer that would crop it at the window's height,
    /// and it is surrounded by buttons that have no business on paper. A document
    /// taller than the page is scaled down to fit rather than silently cut.
    /// </remarks>
    private Grid Page(double width, double height)
    {
        Border sheet = new()
        {
            Padding = new Thickness(PageMargin),
            Child = new ProtocolDocument { DataContext = Document.DataContext },
        };

        Viewbox fitted = new()
        {
            Child = sheet,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            VerticalAlignment = VerticalAlignment.Top,
        };

        Grid page = new()
        {
            Width = width,
            Height = height,
            Background = (Brush)FindResource("Brush.Paper"),
            Children = { fitted },
        };

        page.Measure(new Size(width, height));
        page.Arrange(new Rect(0.0, 0.0, width, height));
        page.UpdateLayout();

        return page;
    }
}
