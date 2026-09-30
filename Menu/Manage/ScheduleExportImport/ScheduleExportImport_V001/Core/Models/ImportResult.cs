namespace Revit26_Plugin.ScheduleExportImport.V001.Core.Models
{
    public class ImportResult
    {
        public int ElementsUpdated { get; set; }
        public int ParametersWritten { get; set; }
        public int ElementsNotFound { get; set; }
        public int ParametersSkipped { get; set; }
        public int Errors { get; set; }
        public string ErrorDetail { get; set; } = string.Empty;
    }
}
