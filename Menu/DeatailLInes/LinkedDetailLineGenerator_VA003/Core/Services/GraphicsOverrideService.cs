using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>
    /// Applies per-mapping color overrides to generated Detail Lines using
    /// View.SetElementOverrides (OverrideGraphicSettings) — a view-specific override
    /// that never touches the project's shared GraphicsStyle/Line Style color, per
    /// spec Section 12's explicit requirement.
    /// </summary>
    public class GraphicsOverrideService
    {
        private static readonly Dictionary<string, Color> NamedColors = new()
        {
            ["Red"] = new Color(217, 83, 79),
            ["Blue"] = new Color(45, 108, 223),
            ["Green"] = new Color(46, 158, 100),
            ["Yellow"] = new Color(224, 146, 46),
            ["Orange"] = new Color(230, 126, 34),
            ["Purple"] = new Color(155, 89, 182),
            ["Black"] = new Color(0, 0, 0),
        };

        /// <summary>Applies the named color override to the element in the given view.
        /// "None" or an unrecognized name is a no-op (element keeps its Detail Line
        /// Style's default color). Must be called inside an active transaction.</summary>
        public void ApplyColorOverride(Document doc, View view, ElementId elementId, string colorName, Action<string>? onWarning = null)
        {
            if (string.IsNullOrWhiteSpace(colorName) || colorName == "None")
                return;

            if (!NamedColors.TryGetValue(colorName, out Color color))
            {
                onWarning?.Invoke($"Unrecognized color override '{colorName}' — skipped, element keeps default line color.");
                return;
            }

            try
            {
                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                ogs.SetProjectionLineColor(color);
                // Detail Curves are 2D annotation-plane elements; projection line
                // color is the relevant override (cut line color does not apply).
                view.SetElementOverrides(elementId, ogs);
            }
            catch (Exception ex)
            {
                onWarning?.Invoke($"Failed to apply color override '{colorName}': {ex.Message}");
            }
        }

        public IEnumerable<string> AvailableColorNames => NamedColors.Keys;
    }
}
