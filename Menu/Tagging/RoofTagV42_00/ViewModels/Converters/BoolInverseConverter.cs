using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26.RoofTagV42.ViewModels.Converters
{
    // Original BoolInverseConverter
    public class BoolInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return value;
        }
    }

    // New converters
    public class BoolToButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isProcessing)
                return isProcessing ? "Processing..." : "Run Tagging";
            return "Run Tagging";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToButtonColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isProcessing)
            {
                if (isProcessing)
                    return new SolidColorBrush(Color.FromRgb(0x2B, 0x91, 0xAF)); // Teal for processing
                return new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)); // Blue for ready
            }
            return new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isProcessing)
                return isProcessing ? "? Processing..." : "? Ready";
            return "? Ready";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}