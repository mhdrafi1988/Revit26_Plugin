using Revit26_Plugin.SheetViewArrange.V001.Core.Layout;
using Revit26_Plugin.SheetViewArrange.V001.Core.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.SheetViewArrange.V001.Core.Services
{
    /// <summary>
    /// What the Views grid shows. Display only: a row hidden by the filter is still arranged if it
    /// is ticked. No WPF or Revit types, so it can be tested on its own.
    /// </summary>
    public sealed class ArrangeGridFilter
    {
        private static readonly IReadOnlySet<string> NoStrings = new HashSet<string>();
        private static readonly IReadOnlySet<ArrangeStatus> NoStatuses = new HashSet<ArrangeStatus>();

        /// <summary>The filter that shows everything.</summary>
        public static ArrangeGridFilter None { get; } = new();

        /// <summary>
        /// Text to find in detail number, view name or view type, case-insensitive. Several words
        /// must all be found. Blank means no text filter.
        /// </summary>
        public string Search { get; init; } = "";

        /// <summary>View types to show; empty means every type.</summary>
        public IReadOnlySet<string> ViewTypes { get; init; } = NoStrings;

        /// <summary>Statuses to show; empty means every status.</summary>
        public IReadOnlySet<ArrangeStatus> Statuses { get; init; } = NoStatuses;

        /// <summary>Row labels to show (see <see cref="ArrangeRow.RowLabel"/>); empty means every row.</summary>
        public IReadOnlySet<string> Rows { get; init; } = NoStrings;

        /// <summary>Show by tick state.</summary>
        public ArrangeShowMode Show { get; init; } = ArrangeShowMode.All;

        /// <summary>True when the filter hides anything at all.</summary>
        public bool IsActive =>
            !string.IsNullOrWhiteSpace(Search) || ViewTypes.Count > 0 || Statuses.Count > 0
            || Rows.Count > 0 || Show != ArrangeShowMode.All;

        /// <summary>True when <paramref name="row"/> passes every part of the filter.</summary>
        public bool Matches(ArrangeRow row)
        {
            if (row == null) return false;

            if (Show == ArrangeShowMode.Ticked && !row.IsTicked) return false;
            if (Show == ArrangeShowMode.Unticked && row.IsTicked) return false;
            if (ViewTypes.Count > 0 && !ViewTypes.Contains(row.ViewTypeName ?? "")) return false;
            if (Statuses.Count > 0 && !Statuses.Contains(row.Status)) return false;
            if (Rows.Count > 0 && !Rows.Contains(row.RowLabel)) return false;

            if (!string.IsNullOrWhiteSpace(Search))
            {
                string haystack = $"{row.DetailNumber} {row.ViewName} {row.ViewTypeName}";
                foreach (var word in Search.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                    if (haystack.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0)
                        return false;
            }

            return true;
        }
    }

    /// <summary>Group names and row ordering for the Views grid. Display only.</summary>
    public static class ArrangeGridSort
    {
        /// <summary>Group header text of <paramref name="row"/> for <paramref name="groupBy"/> ("" when not grouped).</summary>
        public static string GroupName(ArrangeRow row, ArrangeGroupBy groupBy) => groupBy switch
        {
            ArrangeGroupBy.ViewType => string.IsNullOrWhiteSpace(row.ViewTypeName) ? "(no type)" : row.ViewTypeName,
            ArrangeGroupBy.Status => row.StatusLabel,
            ArrangeGroupBy.Row => row.GroupRowName,
            _ => ""
        };

        /// <summary>
        /// Order of rows: first by group (so groups come out in a sensible order), then by each
        /// sort key, then by reading order. Empty cells (no "#" or Row) always sort last.
        /// </summary>
        public static Comparison<ArrangeRow> CreateComparison(ArrangeGroupBy groupBy, IReadOnlyList<ArrangeSortKey> keys)
        {
            var sort = keys == null ? Array.Empty<ArrangeSortKey>() : new List<ArrangeSortKey>(keys).ToArray();
            return (a, b) =>
            {
                int c = CompareGroup(a, b, groupBy);
                if (c != 0) return c;

                foreach (var key in sort)
                {
                    c = CompareColumn(a, b, key);
                    if (c != 0) return c;
                }

                return a.ReadingKey.CompareTo(b.ReadingKey);
            };
        }

        private static int CompareGroup(ArrangeRow a, ArrangeRow b, ArrangeGroupBy groupBy) => groupBy switch
        {
            ArrangeGroupBy.ViewType => string.Compare(GroupName(a, groupBy), GroupName(b, groupBy), StringComparison.OrdinalIgnoreCase),
            ArrangeGroupBy.Status => StatusRank(a.Status).CompareTo(StatusRank(b.Status)),
            ArrangeGroupBy.Row => CompareNullable(a.Row, b.Row, descending: false),
            _ => 0
        };

        private static int CompareColumn(ArrangeRow a, ArrangeRow b, ArrangeSortKey key)
        {
            int sign = key.Descending ? -1 : 1;
            switch (key.Column)
            {
                case ArrangeSortColumn.Order:
                    return CompareNullable(a.Order, b.Order, key.Descending);
                case ArrangeSortColumn.Row:
                    return CompareNullable(a.Row, b.Row, key.Descending);
                case ArrangeSortColumn.DetailNumber:
                    return CompareBlankLast(a.DetailNumber, b.DetailNumber, key.Descending, DetailNumberComparer.Instance);
                case ArrangeSortColumn.ViewName:
                    return sign * string.Compare(a.ViewName, b.ViewName, StringComparison.OrdinalIgnoreCase);
                case ArrangeSortColumn.ViewType:
                    return sign * string.Compare(a.ViewTypeName, b.ViewTypeName, StringComparison.OrdinalIgnoreCase);
                case ArrangeSortColumn.Size:
                    return sign * a.SizeArea.CompareTo(b.SizeArea);
                case ArrangeSortColumn.Status:
                    return sign * StatusRank(a.Status).CompareTo(StatusRank(b.Status));
                default:
                    return 0;
            }
        }

        /// <summary>Move, In place, Doesn't fit, Skipped — the order groups and sorts by status use.</summary>
        private static int StatusRank(ArrangeStatus status) => status switch
        {
            ArrangeStatus.Move => 0,
            ArrangeStatus.InPlace => 1,
            ArrangeStatus.DoesNotFit => 2,
            _ => 3
        };

        private static int CompareNullable(int? a, int? b, bool descending)
        {
            if (a == null || b == null)
                return a == b ? 0 : (a == null ? 1 : -1); // empty last, whatever the direction
            return (descending ? -1 : 1) * a.Value.CompareTo(b.Value);
        }

        private static int CompareBlankLast(string a, string b, bool descending, IComparer<string> comparer)
        {
            bool aBlank = string.IsNullOrWhiteSpace(a), bBlank = string.IsNullOrWhiteSpace(b);
            if (aBlank || bBlank)
                return aBlank == bBlank ? 0 : (aBlank ? 1 : -1);
            return (descending ? -1 : 1) * comparer.Compare(a, b);
        }
    }
}
