using System.Collections.Generic;

namespace WorksetManager_01.Models
{
    /// <summary>
    /// Represents a single workset with its element summary.
    /// </summary>
    public class WorksetSummaryItem
    {
        public int WorksetId { get; set; }
        public string WorksetName { get; set; } = string.Empty;
        public int TotalElements { get; set; }
        public bool IsEditable { get; set; }
        public bool IsOpen { get; set; }

        /// <summary>Key = Type Name, Value = count of elements of that type.</summary>
        public Dictionary<string, int> ByTypeName { get; set; } = new();

        /// <summary>Formatted breakdown string for display in the DataGrid.</summary>
        public string TypeBreakdown
        {
            get
            {
                if (ByTypeName.Count == 0) return "—";
                var parts = new List<string>();
                foreach (var kvp in ByTypeName)
                    parts.Add($"{kvp.Key}: {kvp.Value}");
                return string.Join(" | ", parts);
            }
        }

        public string StatusLabel => IsOpen ? "Open" : "Closed";
        public string EditableLabel => IsEditable ? "✔" : "✘";
    }
}
