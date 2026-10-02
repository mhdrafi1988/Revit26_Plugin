namespace Revit26_Plugin.ScheduleExportImport.V003.Core.Models
{
    public enum ImportChangeStatus
    {
        Change,
        NotFound,
        Duplicate,
        ReadOnly,
        TypeParameter,
        MissingFromFile,
        Applied,
        Failed
    }

    /// <summary>One line of the import preview: a single parameter on a single element,
    /// or an element-level issue (not found, duplicate, missing) with ParameterName "—".</summary>
    public class ImportChange
    {
        public long ElementId { get; set; }
        public string ElementName { get; set; } = string.Empty;
        public string ParameterName { get; set; } = "—";
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public ImportChangeStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
