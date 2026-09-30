using Autodesk.Revit.DB;

namespace Revit26_Plugin.ScheduleExportImport.V001.Core.Models
{
    /// <summary>
    /// Describes one column in a schedule view: the display header,
    /// the underlying parameter, and whether it is writable on import.
    /// </summary>
    public class ScheduleFieldInfo
    {
        public string Header { get; set; }
        public ScheduleFieldId FieldId { get; set; }
        public StorageType StorageType { get; set; }
        public bool IsReadOnly { get; set; }
    }
}
