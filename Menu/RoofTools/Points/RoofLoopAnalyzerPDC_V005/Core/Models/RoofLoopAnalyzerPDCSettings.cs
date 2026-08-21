// RoofLoopAnalyzerPDCSettings.cs
// Namespace: Revit26_Plugin.RoofLoopAnalyzerPDC.V005
// Persisted as JSON at %AppData%\Revit26_Plugin\RoofLoopAnalyzerPDC\settings.json
// (System.Text.Json POCO, per project settings rule). Loaded when the window
// opens, saved when it closes and after every successful Run.

namespace Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Core.Models
{
    /// <summary>Per-tool persisted user settings. Plain POCO — no Revit types.</summary>
    public class RoofLoopAnalyzerPDCSettings
    {
        /// <summary>Folder last used for log / report export.</summary>
        public string ExportFolderPath { get; set; }

        /// <summary>Free-form per-tool values, kept as a dictionary so adding a
        /// setting never breaks an existing settings.json on disk.</summary>
        public System.Collections.Generic.Dictionary<string, string> Values { get; set; }
            = new System.Collections.Generic.Dictionary<string, string>();
    }
}
