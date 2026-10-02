using System.Collections.Generic;

namespace Revit26_Plugin.ScheduleExportImport.V004.Core.Models
{
    /// <summary>Contents of an edited export workbook, as read back by ScheduleExcelService.</summary>
    public class ExcelImportFile
    {
        public string FilePath { get; set; } = string.Empty;
        /// <summary>Schedule name recorded on the "Read Me" sheet at export; empty if absent.</summary>
        public string ScheduleName { get; set; } = string.Empty;
        public List<string> Headers { get; set; } = new List<string>();
        public List<ScheduleRow> Rows { get; set; } = new List<ScheduleRow>();
    }
}
