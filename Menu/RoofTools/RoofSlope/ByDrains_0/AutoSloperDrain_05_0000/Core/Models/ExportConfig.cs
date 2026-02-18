namespace Revit26_Plugin.AutoSlope.V5_00.Core.Models
{
    public class ExportConfig
    {
        public string ExportPath { get; set; } = string.Empty;
        public bool ExportToCsv { get; set; } = true;
        public bool IncludeVertexDetails { get; set; } = true;
        public string FileNamePrefix { get; set; } = "AutoSlope_V5";
    }
}