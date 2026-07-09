// File: DrainSelectionSignature.cs
// Location: Core/Models/
// V003 addition: carries enough info to re-identify a user-selected drain
// after DrainDetectionService re-runs with fresh handles, WITHOUT relying on
// index position (which could silently point at the wrong drain if the roof
// was edited while the modeless window was open).

namespace Revit26_Plugin.AutoSlopeByDrain.V004.Core.Models
{
    public class DrainSelectionSignature
    {
        /// <summary>Center point in Revit internal units (feet).</summary>
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double CenterZ { get; set; }

        /// <summary>Width/Height in millimeters — used as a secondary check alongside position.</summary>
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
