// File: ExportConfig.cs
// Location: Core/Models/
// Changes vs CSV version:
//   REMOVED  IncludeVertexDetails toggle (per approved UI — export now
//            always writes the detailed vertex CSV + summary CSV).

namespace Revit26_Plugin.AutoSlopeByDrain.V004.Core.Models
{
    public class ExportConfig
    {
        public string ExportPath { get; set; } = string.Empty;
        public bool ExportToCsv { get; set; } = true;
        public string FileNamePrefix { get; set; } = "DrainDetection_";
        public bool IncludeTimestamp { get; set; } = true;
    }
}
