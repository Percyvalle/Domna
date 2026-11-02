using System.Windows;
using System.Windows.Media;
using Domna.Domain.Models;

namespace Domna.App.Controls;

/// <summary>
/// The furnace top seen from above: one disc, coloured from the axis outwards by
/// the three computed temperatures.
/// </summary>
/// <remarks>
/// The ore ridge sits at 45% of the radius, which is where the design places it and
/// where the charge actually piles up. Before the first calculation the disc is
/// hatched rather than coloured — an empty result must not look like a cold furnace.
/// </remarks>
internal sealed class FurnaceHeatmap : FrameworkElement
{
    /// <summary>The profile to paint, or <see langword="null"/> before the first calculation.</summary>
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
        nameof(Profile),
        typeof(TemperatureProfile?),
        typeof(FurnaceHeatmap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double OreRidgeRadius = 0.45;

    public TemperatureProfile? Profile
    {
        get => (TemperatureProfile?)GetValue(ProfileProperty);
        set => SetValue(ProfileProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        double radius = Math.Min(ActualWidth, ActualHeight) / 2.0;
        if (radius <= 0.0)
        {
            return;
        }

        Point center = new(ActualWidth / 2.0, ActualHeight / 2.0);
        Pen edge = new((Brush)FindResource("Brush.BorderStrong"), 1.0);
        edge.Freeze();

        drawingContext.DrawEllipse(Fill(), edge, center, radius - 0.5, radius - 0.5);
    }

    private Brush Fill()
    {
        if (Profile is not { } profile)
        {
            return Hatch();
        }

        RadialGradientBrush brush = new()
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5,
            GradientStops =
            [
                new GradientStop(HeatScale.At(this, profile.Center), 0.0),
                new GradientStop(HeatScale.At(this, profile.OreRidge), OreRidgeRadius),
                new GradientStop(HeatScale.At(this, profile.Periphery), 1.0),
            ],
        };

        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }

    private DrawingBrush Hatch()
    {
        GeometryDrawing stripe = new(
            (Brush)FindResource("Brush.Panel"),
            null,
            new RectangleGeometry(new Rect(0, 0, 6, 12)));

        DrawingBrush brush = new(new DrawingGroup
        {
            Children =
            [
                new GeometryDrawing((Brush)FindResource("Brush.Column"), null, new RectangleGeometry(new Rect(0, 0, 12, 12))),
                stripe,
            ],
        })
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 12, 12),
            ViewportUnits = BrushMappingMode.Absolute,
            Transform = new RotateTransform(45.0),
        };

        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }
}
