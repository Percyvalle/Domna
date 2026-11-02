using System.Globalization;
using System.Windows.Data;

namespace Domna.App.Converters;

/// <summary>
/// Moves one parameter value between the model and its text box, at the precision
/// the catalogue gives that parameter.
/// </summary>
/// <remarks>
/// This is the culture boundary for input. What the operator reads is written in the
/// current culture, so a Russian operator sees and may type a decimal comma; what is
/// typed is accepted in either the current or the invariant form, because a value
/// pasted from a report or a spreadsheet arrives with a point. A value that parses to
/// nothing leaves the model untouched rather than resetting it to zero.
/// </remarks>
internal sealed class ParameterValueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [double value, int decimals])
        {
            return string.Empty;
        }

        return value.ToString($"F{decimals}", CultureInfo.CurrentCulture);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        string text = (value as string ?? string.Empty).Trim();

        bool parsed =
            double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double number)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);

        // Second slot is the precision, which is read-only: nothing flows back to it.
        return [parsed ? number : Binding.DoNothing, Binding.DoNothing];
    }
}
