using Revit26_Plugin.SheetViewArrange.V001.UI.ViewModels;
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Revit26_Plugin.SheetViewArrange.V001.UI.Converters
{
    /// <summary>
    /// Tick state of a group header: true when every tickable view in the group is ticked, false
    /// when none is, null (the "–" box) when some are. Bound as { group, tick revision } so it
    /// is re-read whenever any tick changes.
    /// </summary>
    public sealed class GroupTickStateConverter : IMultiValueConverter
    {
        /// <inheritdoc/>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 0 || values[0] is not CollectionViewGroup group)
                return false;

            var members = group.Items.OfType<GridRowViewModel>().Where(r => r.CanTick).ToList();
            int ticked = members.Count(r => r.IsTicked);
            if (members.Count == 0 || ticked == 0)
                return false;
            return ticked == members.Count ? true : null;
        }

        /// <inheritdoc/>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>"6 view(s) · 4 ticked" for a group header. Bound like <see cref="GroupTickStateConverter"/>.</summary>
    public sealed class GroupSummaryConverter : IMultiValueConverter
    {
        /// <inheritdoc/>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 0 || values[0] is not CollectionViewGroup group)
                return "";

            var rows = group.Items.OfType<GridRowViewModel>().ToList();
            int ticked = rows.Count(r => r.CanTick && r.IsTicked);
            return $"{rows.Count} view(s) · {ticked} ticked";
        }

        /// <inheritdoc/>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
