using System.Globalization;

namespace Domna.UI.ViewModels;

/// <summary>
/// Every number the operator reads is formatted here.
/// </summary>
/// <remarks>
/// Display uses the current culture, so a Russian operator sees a decimal comma.
/// This is the display side of the boundary only: values that are parsed, stored or
/// exported go through the invariant culture instead, and nothing in this type is
/// suitable for those.
/// </remarks>
internal static class DisplayNumber
{
    /// <summary>Shown where a value has not been computed yet.</summary>
    public const string Missing = "–";

    /// <summary>Decimal places every temperature is shown with.</summary>
    public const int TemperatureDecimals = 1;

    /// <summary>Decimal places every temperature ratio is shown with.</summary>
    public const int RatioDecimals = 3;

    /// <summary>A value with a fixed number of decimals.</summary>
    public static string Fixed(double value, int decimals) =>
        value.ToString($"F{decimals}", CultureInfo.CurrentCulture);

    /// <summary>A temperature in degrees Celsius, or the placeholder when there is none.</summary>
    public static string Temperature(double? celsius) =>
        celsius is { } value ? Fixed(value, TemperatureDecimals) + " °C" : Missing;

    /// <summary>A temperature ratio, or the placeholder when there is none.</summary>
    public static string Ratio(double? ratio) =>
        ratio is { } value ? Fixed(value, RatioDecimals) : Missing;
}
