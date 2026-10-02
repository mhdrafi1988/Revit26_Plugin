using System.Collections.Generic;

namespace Revit26_Plugin.ScheduleExportImport.V002.Core.Models
{
    /// <summary>
    /// One row from a schedule export: the element id and a map of
    /// column header → cell value string.
    /// </summary>
    public class ScheduleRow
    {
        public long ElementId { get; set; }
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
    }
}
