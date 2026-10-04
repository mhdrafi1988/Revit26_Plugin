namespace Revit26_Plugin.ScheduleExportImport.V006.Core.Models
{
    public enum ImportChangeStatus
    {
        Change,
        /// <summary>The model value changed since the file was exported — someone else edited it.
        /// Selectable, but left unticked so their edit isn't overwritten by accident.</summary>
        Conflict,
        /// <summary>Edit to a type parameter. Selectable, unticked by default; applied to the type
        /// (every instance of it changes).</summary>
        TypeParameter,
        /// <summary>Element borrowed by another user or newer in central — can't be edited now.</summary>
        NotEditable,
        NotFound,
        Duplicate,
        ReadOnly,
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
