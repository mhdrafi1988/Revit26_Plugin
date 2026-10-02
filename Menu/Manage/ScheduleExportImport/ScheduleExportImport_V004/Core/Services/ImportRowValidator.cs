using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.ScheduleExportImport.V004.Core.Models;

namespace Revit26_Plugin.ScheduleExportImport.V004.Core.Services
{
    /// <summary>
    /// Resolves repeated Element IDs in an import file. Rows that repeat an ID with identical
    /// values are merged into one; if the repeats disagree the element is held back entirely,
    /// since there is no safe way to pick which row the user meant.
    /// </summary>
    public static class ImportRowValidator
    {
        public class Result
        {
            public List<ScheduleRow> UniqueRows { get; } = new List<ScheduleRow>();
            public List<ImportChange> Conflicts { get; } = new List<ImportChange>();
            public int MergedDuplicateRows { get; set; }
        }

        public static Result Deduplicate(IEnumerable<ScheduleRow> rows)
        {
            var result = new Result();
            foreach (var group in rows.GroupBy(r => r.ElementId))
            {
                var list = group.ToList();
                if (list.Count == 1)
                {
                    result.UniqueRows.Add(list[0]);
                    continue;
                }

                var first = list[0];
                bool allSame = list.Skip(1).All(r => SameValues(first, r));
                if (allSame)
                {
                    result.UniqueRows.Add(first);
                    result.MergedDuplicateRows += list.Count - 1;
                }
                else
                {
                    var differing = first.Values.Keys
                        .Where(k => list.Any(r => !r.Values.TryGetValue(k, out var v) || v != first.Values[k]))
                        .ToList();
                    result.Conflicts.Add(new ImportChange
                    {
                        ElementId = group.Key,
                        Status = ImportChangeStatus.Duplicate,
                        Message = $"Appears in {list.Count} rows with different values ({string.Join(", ", differing.Take(3))}" +
                                  (differing.Count > 3 ? ", …" : "") + "). Not imported — keep one row per Element ID."
                    });
                }
            }
            return result;
        }

        private static bool SameValues(ScheduleRow a, ScheduleRow b)
            => a.Values.Count == b.Values.Count
               && a.Values.All(kv => b.Values.TryGetValue(kv.Key, out var v) && v == kv.Value);
    }
}
