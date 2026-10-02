using System.Collections.Generic;

namespace Revit26_Plugin.ScheduleExportImport.V002.Core.Models
{
    public class ImportAnalysis
    {
        public List<ImportChange> Items { get; set; } = new List<ImportChange>();
        public int RowsInFile { get; set; }
        public int ElementsMatched { get; set; }
        public int UnchangedValues { get; set; }
        public int DuplicateRowsMerged { get; set; }
    }
}
