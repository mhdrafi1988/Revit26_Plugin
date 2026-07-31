using Revit26_Plugin.Shared.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.RoofCreationIsolationTest.V001.UI.Views
{
    /// <summary>
    /// Converts a LogLevel value to a display Brush for the execution log.
    /// Deliberately kept local to this tool (not in the shared Revit26_Plugin.Shared/Converters.cs)
    /// per explicit instruction to keep all styling and converters self-contained in this file/tool.
    /// This means it is a parallel, tool-specific definition rather than the shared one — if the
    /// shared LogLevelToColorConverter's color mapping changes later, this one will not follow
    /// automatically.
    /// </summary>
    public class LogLevelToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush InfoBrush = new(Color.FromRgb(0x64, 0xD2, 0xFF));
        private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(0xF0, 0xAE, 0x5C));
        private static readonly SolidColorBrush ErrorBrush = new(Color.FromRgb(0xF0, 0x83, 0x7D));
        private static readonly SolidColorBrush SuccessBrush = new(Color.FromRgb(0x4C, 0xC1, 0x8A));
        private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(0xEA, 0xF1, 0xF8));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Info => InfoBrush,
                    LogLevel.Warning => WarningBrush,
                    LogLevel.Error => ErrorBrush,
                    LogLevel.Success => SuccessBrush,
                    _ => DefaultBrush
                };
            }

            return DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("LogLevelToColorConverter is one-way only.");
        }
    }
}
