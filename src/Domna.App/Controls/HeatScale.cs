using System.Windows;
using System.Windows.Media;

namespace Domna.App.Controls;

/// <summary>
/// Turns a top gas temperature into a colour on the cold-to-hot scale.
/// </summary>
/// <remarks>
/// The stops live in <c>Themes/Palette.xaml</c> as <c>Heat.Ramp</c>; only the
/// mapping from degrees to a position on that ramp lives here. The window
/// 100–300 °C is the range the fitted region actually produces — outside it the
/// scale saturates rather than inventing a colour nothing else on screen uses.
/// </remarks>
internal static class HeatScale
{
    private const double ColdCelsius = 100.0;
    private const double HotCelsius = 300.0;

    /// <summary>The colour of <paramref name="celsius"/> on the scale <paramref name="source"/> can see.</summary>
    public static Color At(FrameworkElement source, double celsius)
    {
        GradientStopCollection ramp = Ramp(source);
        double position = Math.Clamp((celsius - ColdCelsius) / (HotCelsius - ColdCelsius), 0.0, 1.0);

        GradientStop lower = ramp[0];
        foreach (GradientStop stop in ramp)
        {
            if (position <= stop.Offset)
            {
                return Blend(lower, stop, position);
            }

            lower = stop;
        }

        return lower.Color;
    }

    private static GradientStopCollection Ramp(FrameworkElement source) =>
        (GradientStopCollection)source.FindResource("Heat.Ramp");

    private static Color Blend(GradientStop from, GradientStop to, double position)
    {
        double span = to.Offset - from.Offset;
        double t = span <= 0.0 ? 0.0 : (position - from.Offset) / span;

        return Color.FromArgb(
            Channel(from.Color.A, to.Color.A, t),
            Channel(from.Color.R, to.Color.R, t),
            Channel(from.Color.G, to.Color.G, t),
            Channel(from.Color.B, to.Color.B, t));
    }

    private static byte Channel(byte from, byte to, double t) =>
        (byte)Math.Round(from + ((to - from) * t));
}
