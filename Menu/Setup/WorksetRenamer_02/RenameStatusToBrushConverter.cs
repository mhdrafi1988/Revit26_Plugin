using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.WorksetRenamer_01.ViewModels
{
    /// <summary>
    /// Converts a <see cref="RenameStatus"/> enum value to a
    /// <see cref="SolidColorBrush"/> using the SharedStyles color tokens.
    /// Pending   → TextTertiary  #AEAEB2
    /// Renamed   → ColorSuccess  #34C759
    /// Unchanged → TextSecondary #8E8E93
    /// Error     → ColorDanger   #FF453A
    /// </summary>
    public class RenameStatusToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Pending = new SolidColorBrush(Color.FromRgb(0xAE, 0xAE, 0xB2));
        private static readonly SolidColorBrush Renamed = new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59));
        private static readonly SolidColorBrush Unchanged = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93));
        private static readonly SolidColorBrush Error = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RenameStatus status)
                return status switch
                {
                    RenameStatus.Pending => Pending,
                    RenameStatus.Renamed => Renamed,
                    RenameStatus.Unchanged => Unchanged,
                    RenameStatus.Error => Error,
                    _ => Pending
                };
            return Pending;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a bool to a status-dot brush.
    /// true  → ColorSuccess #34C759 (green)
    /// false → TextTertiary #AEAEB2 (grey)
    /// Used for the read-only Open and Editable indicator columns.
    /// </summary>
    public class BoolToIndicatorBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Active = new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59));
        private static readonly SolidColorBrush Inactive = new SolidColorBrush(Color.FromRgb(0xAE, 0xAE, 0xB2));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Active : Inactive;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}