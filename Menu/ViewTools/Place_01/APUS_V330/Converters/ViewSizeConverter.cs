// File: Converters/ViewSizeConverter.cs
using Autodesk.Revit.DB;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V330.Converters
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
                        double width  = (bb.Max.X - bb.Min.X) / view.Scale * 304.8;
                        double height = (bb.Max.Y - bb.Min.Y) / view.Scale * 304.8;
                        return $"{width:F0} x {height:F0} mm";
                    }
                }
                catch { /* ignore */ }
            }
            return "N/A";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
