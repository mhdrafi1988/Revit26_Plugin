using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain_06_00.Core.Models
{
    public class ExportConfig
    {
        // Basic Export Settings
        public string ExportPath { get; set; } = string.Empty;
        public ExportFormat Format { get; set; } = ExportFormat.Excel;
        public bool CreateBackup { get; set; } = true;
        public bool OverwriteExisting { get; set; } = false;
        public string FileNamePrefix { get; set; } = "AutoSlope_";
        public bool IncludeTimestamp { get; set; } = true;

        // CSV Export Specific
        public bool ExportToCsv { get; set; } = true;
        public bool IncludeVertexDetails { get; set; } = true;

        // Data Content Options
        public bool ExportRoofGeometry { get; set; } = true;
        public bool ExportDrainPoints { get; set; } = true;
        public bool ExportSlopeVectors { get; set; } = true;
        public bool ExportCalculatedSlopes { get; set; } = true;
        public bool ExportThresholdCheck { get; set; } = true;
        public bool ExportLogs { get; set; } = true;
        public bool ExportErrorDetails { get; set; } = true;

        // Excel Specific Options
        public bool CreateMultipleSheets { get; set; } = true;
        public bool ApplyFormatting { get; set; } = true;
        public bool IncludeCharts { get; set; } = false;
        public bool FreezeHeaders { get; set; } = true;
        public string ExcelTemplatePath { get; set; } = string.Empty;

        // JSON/XML Specific Options
        public bool PrettyPrint { get; set; } = true;
        public bool IncludeMetadata { get; set; } = true;
        public JsonSerializationFormat JsonFormat { get; set; } = JsonSerializationFormat.Indented;
        public bool CompressOutput { get; set; } = false;

        // CAD/Revit Export Options
        public bool ExportToCAD { get; set; } = false;
        public string CADFormat { get; set; } = "DWG";
        public double CADExportScale { get; set; } = 1.0;
        public bool ExportRevitParameters { get; set; } = true;
        public bool ExportElementIds { get; set; } = true;

        // Report Generation Options
        public bool GenerateReport { get; set; } = true;
        public ReportFormat ReportFormat { get; set; } = ReportFormat.PDF;
        public bool IncludeSummary { get; set; } = true;
        public bool IncludeVisualizations { get; set; } = true;
        public bool IncludeRecommendations { get; set; } = true;

        // Filtering Options
        public double MinSlopeThreshold { get; set; } = 0.0;
        public double MaxSlopeThreshold { get; set; } = 100.0;
        public bool FilterByDrainageArea { get; set; } = false;
        public double MinDrainageArea { get; set; } = 0.0;
        public List<string> IncludedRoofTypes { get; set; } = new List<string>();

        // Performance Options
        public bool UseParallelProcessing { get; set; } = false;
        public int BatchSize { get; set; } = 100;
        public bool CacheResults { get; set; } = true;
        public bool ValidateBeforeExport { get; set; } = true;

        // Notification & Logging
        public bool NotifyOnCompletion { get; set; } = true;
        public string NotificationEmail { get; set; } = string.Empty;
        public LogLevel ExportLogLevel { get; set; } = LogLevel.Info;
        public bool SaveExportLog { get; set; } = true;
        public string LogDirectory { get; set; } = string.Empty;

        // Version Control
        public bool VersionFiles { get; set; } = true;
        public int MaxVersionsToKeep { get; set; } = 10;
        public bool AddDigitalSignature { get; set; } = false;

        // Customization
        public Dictionary<string, object> CustomSettings { get; set; } = new Dictionary<string, object>();
        public string CustomTemplatePath { get; set; } = string.Empty;
        public bool UseCustomMapping { get; set; } = false;
        public string FieldMappingFile { get; set; } = string.Empty;

        // Validation Methods
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ExportPath);
        }

        public string GetDefaultFileName()
        {
            var timestamp = IncludeTimestamp ? $"_{DateTime.Now:yyyyMMdd_HHmmss}" : "";
            return $"{FileNamePrefix}{timestamp}";
        }

        public bool IsCsvExportEnabled => Format == ExportFormat.CSV || ExportToCsv;
    }

    // Supporting Enums
    public enum ExportFormat
    {
        Excel,
        CSV,
        JSON,
        XML,
        PDF,
        DWG,
        RVT,
        TXT
    }

    public enum JsonSerializationFormat
    {
        Compact,
        Indented,
        Formatted
    }

    public enum ReportFormat
    {
        PDF,
        HTML,
        Word,
        Markdown
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }
}