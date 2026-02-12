using Autodesk.Revit.DB;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V315.Views.Converters;

[ValueConversion(typeof(ViewSection), typeof(string))]
public class ViewSizeConverter : IValueConverter
{
    private const double MmPerFoot = 304.8;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ViewSection view)
        {
            try
            {
                var cropBox = view.CropBox;
                if (cropBox != null)
                {
                    double width = (cropBox.Max.X - cropBox.Min.X) / view.Scale * MmPerFoot;
                    double height = (cropBox.Max.Y - cropBox.Min.Y) / view.Scale * MmPerFoot;

                    return $"{width:F0} × {height:F0} mm";
                }
            }
            catch
            {
                // Ignore calculation errors
            }
        }

        return "N/A";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}