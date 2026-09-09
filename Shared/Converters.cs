using Revit26_Plugin.Shared.Models;   // LogLevel — required for LogLevelToColorConverter
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

// ── CONVENTION ────────────────────────────────────────────────────────────────
// These converters live in Revit26_Plugin.Shared.Models (same assembly).
// Reference them in XAML with the FULL assembly-qualified xmlns:
//
//   xmlns:converters="clr-namespace:Revit26_Plugin.Shared.Models;assembly=Revit26_Plugin"
//
// Then instantiate ONLY in the consuming Window's own Resources block:
//
//   <converters:BoolToVisibilityConverter        x:Key="BoolToVisibilityConverter"/>
//   <converters:InverseBoolToVisibilityConverter x:Key="InverseBoolToVisibilityConverter"/>
//   <converters:LogLevelToColorConverter         x:Key="LogLevelToColorConverter"/>
//   <converters:InverseBoolConverter             x:Key="InverseBoolConverter"/>
//
// NEVER declare converter instances inside SharedStyles.xaml — that causes
// "incompatible type" / "does not exist in namespace" errors at design time.
// ──────────────────────────────────────────────────────────────────────────────

namespace Revit26_Plugin.Shared.Models
{
    /// <summary>
    /// Converts a bool value to Visibility (true → Visible, false → Collapsed).
    /// Used for conditional visibility bindings throughout tools.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>
    /// Converts a bool value to its inverse Visibility (true → Collapsed, false → Visible).
    /// Used for inverse visibility bindings (e.g., show when NOT busy).
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && !b) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v != Visibility.Visible;
    }

    /// <summary>
    /// Converts an int value to Visibility (0 → Collapsed, non-zero → Visible).
    /// Used for hiding empty states when lists have items.
    /// </summary>
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is int i && i == 0) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Converts a bool value to its inverse (true → false, false → true).
    /// Used for IsEnabled bindings that should invert state (e.g., disable when busy).
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;
    }

    /// <summary>
    /// Maps LogLevel to a colored brush for log display.
    /// INFO → cyan, WARNING → orange, ERROR → red, SUCCESS → green.
    /// </summary>
    public class LogLevelToColorConverter : IValueConverter
    {
        // Instance brushes — avoids static initializer running off the UI thread in Revit,
        // which caused StaticResourceExtension to throw on first converter instantiation.
        private readonly SolidColorBrush _brushInfo    = new(Color.FromRgb(0x64, 0xD2, 0xFF));
        private readonly SolidColorBrush _brushWarning = new(Color.FromRgb(0xFF, 0x9F, 0x0A));
        private readonly SolidColorBrush _brushError   = new(Color.FromRgb(0xFF, 0x45, 0x3A));
        private readonly SolidColorBrush _brushSuccess = new(Color.FromRgb(0x4C, 0xC1, 0x8A));
        private readonly SolidColorBrush _brushDebug   = new(Color.FromRgb(0x8E, 0x8E, 0x93));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
                return level switch
                {
                    LogLevel.Warning => _brushWarning,
                    LogLevel.Error   => _brushError,
                    LogLevel.Success => _brushSuccess,
                    LogLevel.Debug   => _brushDebug,
                    _                => _brushInfo
                };
            return _brushInfo;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Maps a bool active-state flag to an icon brush (accent blue when true, muted tertiary when false).
    /// Used for toolbar/header icons that indicate an active filter, toggle, or highlighted state.
    /// </summary>
    public class BoolToHeaderIconBrushConverter : IValueConverter
    {
        private readonly SolidColorBrush _active   = new(Color.FromRgb(0x2D, 0x6C, 0xDF)); // BrushAppleBlue
        private readonly SolidColorBrush _inactive = new(Color.FromRgb(0x8F, 0xA3, 0xB8)); // BrushTextTertiary

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? _active : _inactive;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Maps a bool flag to a colored brush (orange when true, dark grey when false).
    /// Used for highlighting mixed worksets or other status flags.
    /// </summary>
    public class MixedWorksetToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush Mixed  = new(Color.FromRgb(0xFF, 0x6B, 0x00));
        private static readonly SolidColorBrush Normal = new(Color.FromRgb(0x1C, 0x1C, 0x1E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Mixed : Normal;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// One-way: true when the bound enum's value name matches the string
    /// ConverterParameter (e.g. an ActiveQuickFilter enum bound with
    /// ConverterParameter="Unplaced"). Compares by ToString() rather than a
    /// concrete enum type so one converter instance works for any enum —
    /// intentionally has no ConvertBack; pair it with Mode=OneWay and drive
    /// the actual state change via a Command, not two-way binding.
    /// </summary>
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value != null && parameter != null
               && string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>
    /// Maps a "ViewTypeGroup"-shaped enum (matched by enum member name, not a
    /// concrete type, so it works across any tool's own versioned copy of that
    /// enum) to a background/foreground brush pair for a category pill.
    /// ConverterParameter selects "Background" or "Foreground" (default: Background).
    /// Unrecognized names fall back to a neutral grey pill.
    /// </summary>
    public class ViewTypeGroupToBrushConverter : IValueConverter
    {
        private static readonly Dictionary<string, (SolidColorBrush Bg, SolidColorBrush Fg)> Palette = new()
        {
            ["SectionOrCallout"] = (new SolidColorBrush(Color.FromRgb(0xE6, 0xF1, 0xFB)), new SolidColorBrush(Color.FromRgb(0x0C, 0x44, 0x7C))),
            ["FloorPlan"]        = (new SolidColorBrush(Color.FromRgb(0xEA, 0xF3, 0xDE)), new SolidColorBrush(Color.FromRgb(0x27, 0x50, 0x0A))),
            ["CeilingPlan"]      = (new SolidColorBrush(Color.FromRgb(0xF5, 0xEC, 0xFA)), new SolidColorBrush(Color.FromRgb(0x5B, 0x2C, 0x82))),
            ["StructuralPlan"]   = (new SolidColorBrush(Color.FromRgb(0xFC, 0xEF, 0xE3)), new SolidColorBrush(Color.FromRgb(0x8A, 0x4B, 0x08))),
            ["AreaPlan"]         = (new SolidColorBrush(Color.FromRgb(0xE3, 0xFC, 0xF4)), new SolidColorBrush(Color.FromRgb(0x0A, 0x6B, 0x4C))),
            ["Elevation"]        = (new SolidColorBrush(Color.FromRgb(0xFA, 0xEE, 0xDA)), new SolidColorBrush(Color.FromRgb(0x63, 0x38, 0x06))),
            ["Drafting"]         = (new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC)), new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A))),
            ["Legend"]           = (new SolidColorBrush(Color.FromRgb(0xFA, 0xEC, 0xE7)), new SolidColorBrush(Color.FromRgb(0x71, 0x2B, 0x13))),
            ["Schedule"]         = (new SolidColorBrush(Color.FromRgb(0xE0, 0xF0, 0xFF)), new SolidColorBrush(Color.FromRgb(0x0D, 0x3A, 0x66))),
        };

        private static readonly (SolidColorBrush Bg, SolidColorBrush Fg) Fallback =
            (new SolidColorBrush(Color.FromRgb(0xEE, 0xF3, 0xF9)), new SolidColorBrush(Color.FromRgb(0x5B, 0x71, 0x85)));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var pair = value != null && Palette.TryGetValue(value.ToString(), out var found) ? found : Fallback;
            return string.Equals(parameter as string, "Foreground", StringComparison.OrdinalIgnoreCase)
                ? pair.Fg
                : pair.Bg;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}
