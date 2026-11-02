using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Domna.Domain.Models;

namespace Domna.App.Controls;

/// <summary>
/// The radial temperature profile: centre, ore ridge, periphery, with the baseline
/// calculation drawn behind it.
/// </summary>
/// <remarks>
/// Drawn rather than charted. Three points on a fixed axis need no charting library,
/// and a hand-drawn plot is the only way to keep the zone colours, the fixed
/// temperature window and the dashed baseline exactly as the design fixes them.
/// The plot fills whatever box it is given: it asks for a modest height and takes
/// the rest, so the card it sits in has no dead space under it. Only the plot area
/// stretches — text, line weights and dot sizes stay where the design put them.
/// </remarks>
internal sealed class RadialProfileChart : FrameworkElement
{
    /// <summary>The profile to plot, or <see langword="null"/> before the first calculation.</summary>
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
        nameof(Profile),
        typeof(TemperatureProfile?),
        typeof(RadialProfileChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The baseline profile drawn behind it, or <see langword="null"/>.</summary>
    public static readonly DependencyProperty BaselineProperty = DependencyProperty.Register(
        nameof(Baseline),
        typeof(TemperatureProfile?),
        typeof(RadialProfileChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double FallbackWidth = 460.0;

    // Asked for, not insisted on: the layout gives the plot whatever the row is tall.
    private const double MinimumHeight = 190.0;

    private const double PadLeft = 46.0;
    private const double PadRight = 14.0;
    private const double PadTop = 12.0;
    private const double PadBottom = 30.0;

    // The window the fitted region produces, rounded outwards to whole gridlines.
    private const double MinCelsius = 80.0;
    private const double MaxCelsius = 320.0;

    private static readonly double[] GridCelsius = [100.0, 150.0, 200.0, 250.0, 300.0];

    // Fractions of the radius: the axis, the ore ridge, the wall.
    private static readonly double[] PositionFractions = [0.0, 0.45, 1.0];

    public TemperatureProfile? Profile
    {
        get => (TemperatureProfile?)GetValue(ProfileProperty);
        set => SetValue(ProfileProperty, value);
    }

    public TemperatureProfile? Baseline
    {
        get => (TemperatureProfile?)GetValue(BaselineProperty);
        set => SetValue(BaselineProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? FallbackWidth : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height)
            ? MinimumHeight
            : Math.Min(availableSize.Height, MinimumHeight);

        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        if (ActualWidth <= PadLeft + PadRight || ActualHeight <= PadTop + PadBottom)
        {
            return;
        }

        DrawGrid(drawingContext);
        DrawSeries(drawingContext);
    }

    private double X(double fraction) => PadLeft + (fraction * (ActualWidth - PadLeft - PadRight));

    private double Y(double celsius)
    {
        double clamped = Math.Clamp(celsius, MinCelsius, MaxCelsius);
        double fraction = (clamped - MinCelsius) / (MaxCelsius - MinCelsius);
        return PadTop + ((1.0 - fraction) * (ActualHeight - PadTop - PadBottom));
    }

    private StreamGeometry Line(TemperatureProfile profile)
    {
        Point[] points =
        [
            new(X(PositionFractions[0]), Y(profile.Center)),
            new(X(PositionFractions[1]), Y(profile.OreRidge)),
            new(X(PositionFractions[2]), Y(profile.Periphery)),
        ];

        StreamGeometry geometry = new();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(points[0], false, false);
            context.PolyLineTo([points[1], points[2]], true, true);
        }

        geometry.Freeze();
        return geometry;
    }

    private void DrawGrid(DrawingContext drawingContext)
    {
        Pen rule = new((Brush)FindResource("Brush.BorderSoft"), 1.0);
        rule.Freeze();

        foreach (double celsius in GridCelsius)
        {
            double y = Y(celsius);
            drawingContext.DrawLine(rule, new Point(PadLeft, y), new Point(ActualWidth - PadRight, y));

            FormattedText label = Label(
                celsius.ToString("F0", CultureInfo.CurrentCulture),
                (Brush)FindResource("Brush.TextFainter"));

            drawingContext.DrawText(label, new Point(PadLeft - 8 - label.Width, y - (label.Height / 2.0)));
        }

        string[] names = ["ЦЕНТР", "ГРЕБЕНЬ", "ПЕРИФЕРИЯ"];
        double[] anchors = [0.0, 0.5, 1.0];

        for (int i = 0; i < names.Length; i++)
        {
            FormattedText label = Label(names[i], (Brush)FindResource("Brush.TextFaint"));
            drawingContext.DrawText(
                label,
                new Point(X(PositionFractions[i]) - (label.Width * anchors[i]), ActualHeight - PadBottom + 10.0));
        }
    }

    private void DrawSeries(DrawingContext drawingContext)
    {
        if (Baseline is { } baseline)
        {
            Pen pen = new((Brush)FindResource("Brush.TextFaint"), 2.0)
            {
                DashStyle = new DashStyle([3.0, 2.5], 0.0),
            };

            pen.Freeze();
            drawingContext.DrawGeometry(null, pen, Line(baseline));
        }

        if (Profile is not { } profile)
        {
            return;
        }

        Pen line = new((Brush)FindResource("Brush.Zone.Center"), 2.5);
        line.Freeze();
        drawingContext.DrawGeometry(null, line, Line(profile));

        Pen halo = new((Brush)FindResource("Brush.Surface"), 2.0);
        halo.Freeze();

        (double Celsius, string Key)[] points =
        [
            (profile.Center, "Brush.Zone.Center"),
            (profile.OreRidge, "Brush.Zone.OreRidge"),
            (profile.Periphery, "Brush.Zone.Periphery"),
        ];

        for (int i = 0; i < points.Length; i++)
        {
            drawingContext.DrawEllipse(
                (Brush)FindResource(points[i].Key),
                halo,
                new Point(X(PositionFractions[i]), Y(points[i].Celsius)),
                5.0,
                5.0);
        }
    }

    private FormattedText Label(string text, Brush brush) => new(
        text,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        new Typeface((FontFamily)FindResource("Font.Mono"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
        11.0,
        brush,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);
}
