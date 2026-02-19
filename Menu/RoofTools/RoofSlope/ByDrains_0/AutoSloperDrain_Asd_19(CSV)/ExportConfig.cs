// File: ExportConfig.cs
// Location: Revit26_Plugin.Asd_19.Models

namespace Revit26_Plugin.Asd_19.Models
{
    public class ExportConfig
    {
        public string ExportPath { get; set; } = string.Empty;
        public bool ExportToCsv { get; set; } = true;
        public bool IncludeVertexDetails { get; set; } = true;
        public string FileNamePrefix { get; set; } = "DrainDetection_";
        public bool IncludeTimestamp { get; set; } = true;
    }
}