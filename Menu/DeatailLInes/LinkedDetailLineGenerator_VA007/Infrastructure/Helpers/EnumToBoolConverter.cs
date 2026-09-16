using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA007.Infrastructure.Helpers
{
    /// <summary>
    /// Binds a RadioButton/ToggleButton's IsChecked to one value of an enum-typed
    /// property — used by the Links panel's status chips (LinkStatusFilter), sort
    /// chips (LinkSortMode) and the Categories panel's "By Category / By Type"
    /// switch (GroupElementsByType is a bool, but the same pattern reads cleanest
    /// applied consistently). ConverterParameter carries the target enum value as
    /// a string (e.g. ConverterParameter=Loaded).
    /// </summary>
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool isChecked || !isChecked || parameter == null)
                return Binding.DoNothing;

            return Enum.Parse(targetType, parameter.ToString()!);
        }
    }
}
