// =======================================================
// File: Services/Interfaces/IDrainDetectionService.cs
// Description: Service contract for drain detection
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByDrain_21.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces
{
    /// <summary>
    /// Detects drains and openings in a roof
    /// </summary>
    public interface IDrainDetectionService
    {
        /// <summary>
        /// Detects drains from a roof face
        /// </summary>
        /// <param name="roof">Roof element</param>
        /// <param name="topFace">Top face of roof</param>
        /// <param name="vertices">Roof vertices</param>
        /// <param name="toleranceMm">Tolerance in millimeters</param>
        /// <param name="enableTolerance">Whether to use tolerance filtering</param>
        /// <returns>List of detected drains</returns>
        List<DrainItem> DetectDrainsFromRoof(
            RoofBase roof,
            Face topFace,
            List<XYZ> vertices,
            double toleranceMm,
            bool enableTolerance);
    }
}