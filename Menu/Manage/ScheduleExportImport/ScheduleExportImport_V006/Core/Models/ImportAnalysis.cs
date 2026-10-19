using System.Collections.Generic;

namespace Revit26_Plugin.ScheduleExportImport.V006.Core.Models
{
    public class ImportAnalysis
    {
        public List<ImportChange> Items { get; set; } = new List<ImportChange>();
        public int RowsInFile { get; set; }
        public int ElementsMatched { get; set; }
        public int UnchangedValues { get; set; }
        public int DuplicateRowsMerged { get; set; }
        /// <summary>File columns that matched no parameter on any matched element (renamed header,
        /// calculated/formula column, …) — their values are ignored.</summary>
        public List<string> UnmatchedHeaders { get; set; } = new List<string>();
    }
}
