using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace Revit22_Plugin.AutoRoofSections.Models
{
    public class SectionSettings
    {
        // UI values
        public string Prefix { get; set; }
        public int Scale { get; set; }
        public double MinEdgeLengthMm { get; set; }
        public bool IncludeTimestamp { get; set; }
        public string DirectionMode { get; set; }

        // Template
        public View SelectedViewTemplate { get; set; }

        // Revit references
        public UIDocument Uidoc { get; set; }
        public UIApplication Uiapp { get; set; }

        // Logger
        public Action<string> LogAction { get; set; }
    }
}
