using Autodesk.Revit.DB;

namespace Revit26_Plugin.ScheduleExportImport.V002.Core.Models
{
    /// <summary>
    /// Summary item shown in the schedule picker list.
    /// </summary>
    public class ScheduleViewInfo
    {
        public ElementId ViewId { get; set; }
        public string Name { get; set; }
        public string CategoryName { get; set; }
        public int RowCount { get; set; }
        public int FieldCount { get; set; }

        public string Display => $"{Name}  ({CategoryName})";
        public string RowCountDisplay => $"{RowCount} rows, {FieldCount} fields";
    }
}
