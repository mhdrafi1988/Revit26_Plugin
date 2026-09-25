using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.WorksetRenamer.FX03.ViewModels
{
    /// <summary>
    /// Converts a <see cref="RowRenameStatus"/> to a status-dot/text brush.
    /// Pending → TextTertiary #AEAEB2, Renamed/Created → ColorSuccess #34C759,
    /// Unchanged → TextSecondary #8E8E93, Error → ColorDanger #FF453A.
    /// </summary>
    public class RowStatusToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Pending = new SolidColorBrush(Color.FromRgb(0xAE, 0xAE, 0xB2));
        private static readonly SolidColorBrush Success = new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59));
        private static readonly SolidColorBrush Unchanged = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93));
        private static readonly SolidColorBrush Error = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RowRenameStatus status)
                return status switch
                {
                    RowRenameStatus.Pending => Pending,
                    RowRenameStatus.Renamed => Success,
                    RowRenameStatus.Created => Success,
                    RowRenameStatus.Unchanged => Unchanged,
                    RowRenameStatus.Error => Error,
                    _ => Pending
                };
            return Pending;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Converts a <see cref="RowKind"/> to its status-icon glyph/brush pairing (grid row accent).</summary>
    public class RowKindToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush RenameBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x7E, 0x1E));
        private static readonly SolidColorBrush CreateNewBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x7A, 0x52));
        private static readonly SolidColorBrush UnmatchedBrush = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
        private static readonly SolidColorBrush InvalidBrush = new SolidColorBrush(Color.FromRgb(0xB2, 0x3B, 0x3B));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RowKind kind)
                return kind switch
                {
                    RowKind.Rename => RenameBrush,
                    RowKind.CreateNew => CreateNewBrush,
                    RowKind.Unmatched => UnmatchedBrush,
                    RowKind.Invalid => InvalidBrush,
                    _ => UnmatchedBrush
                };
            return UnmatchedBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Converts a bool duplicate-warning flag to an amber/transparent brush for row highlighting.</summary>
    public class DuplicateWarningToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Warning = new SolidColorBrush(Color.FromRgb(0xC9, 0x8A, 0x00));
        private static readonly SolidColorBrush None = new SolidColorBrush(Colors.Transparent);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Warning : None;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
