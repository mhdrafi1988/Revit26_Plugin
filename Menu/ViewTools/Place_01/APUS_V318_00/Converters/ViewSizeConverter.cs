// File: ViewSizeConverter.cs
using Autodesk.Revit.DB;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V318.Converters
{
    public class ViewSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ViewSection view)
            {
                try
                {
                    var bb = view.CropBox;
                    if (bb != null)
                    {
                        double width = (bb.Max.X - bb.Min.X) / view.Scale;
                        double height = (bb.Max.Y - bb.Min.Y) / view.Scale;

                        // Convert to mm for display
                        width *= 304.8;
                        height *= 304.8;

                        return $"{width:F0} × {height:F0} mm";
                    }
                }
                catch
                {
                    // Ignore errors
                }
            }

            return "N/A";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}