using System;

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Models
{
    public class ExportConfig
    {
        /// <summary>Absolute folder path where Excel files are saved.</summary>
        public string ExportPath { get; set; } = string.Empty;

        /// <summary>Master switch: write any Excel files at all.</summary>
        public bool ExportToExcel { get; set; } = true;

        /// <summary>
        /// When true, the engine also writes the detailed multi-sheet workbook
        /// (Summary / Drain Points / Vertices / Statistics).
        /// When false, only the compact single-sheet workbook is written.
        /// </summary>
        public bool IncludeVertexDetails { get; set; } = true;

        /// <summary>Prefix prepended to the summary-only export filename.</summary>
        public string FileNamePrefix { get; set; } = "AutoSlope_";

        /// <summary>Append a yyyyMMdd_HHmmss timestamp to summary-only filenames.</summary>
        public bool IncludeTimestamp { get; set; } = true;

        public bool IsValid() => !string.IsNullOrWhiteSpace(ExportPath);

        public string GetDefaultFileName()
        {
            string timestamp = IncludeTimestamp
                ? $"_{DateTime.Now:yyyyMMdd_HHmmss}"
                : string.Empty;
            return $"{FileNamePrefix}{timestamp}";
        }
    }
}
