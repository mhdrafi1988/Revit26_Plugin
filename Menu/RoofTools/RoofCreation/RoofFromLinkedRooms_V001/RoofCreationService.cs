using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofFromLinkedRooms.V001
{
    /// <summary>
    /// Creates a flat FootPrintRoof from one room's boundary loops (outer loop first, then
    /// inner loops as openings). Every parameter is validated and logged before the API call
    /// because NewFootPrintRoof has thrown ArgumentNullException with no ParamName.
    /// </summary>
    public static class RoofCreationService
    {
        /// <summary>
        /// Creates the roof and cuts one opening per inner loop.
        /// Returns the new roof id and the number of openings actually cut.
        /// Throws <see cref="ArgumentException"/> / <see cref="InvalidOperationException"/> on invalid input.
        /// </summary>
        public static ElementId Create(
            Document doc,
            IList<CurveLoop> loops,
            ElementId roofTypeId,
            Level level,
            Action<LogLevel, string> log,
            out int openingsCut)
        {
            openingsCut = 0;

            if (doc == null || !doc.IsValidObject)
                throw new ArgumentException("Document is null or invalid.");

            if (roofTypeId == null || roofTypeId == ElementId.InvalidElementId)
                throw new ArgumentException($"Invalid roofTypeId: {roofTypeId?.Value}", nameof(roofTypeId));

            var roofType = doc.GetElement(roofTypeId) as RoofType;
            if (roofType == null || !roofType.IsValidObject)
                throw new ArgumentException($"RoofType with ID {roofTypeId.Value} not found or not a RoofType.", nameof(roofTypeId));

            CompoundStructure cs = roofType.GetCompoundStructure();
            if (cs == null)
                throw new ArgumentException(
                    $"Roof type '{roofType.Name}' has no compound structure (likely Sloped Glazing). Pick a Basic Roof type.");

            if (level == null || !level.IsValidObject)
                throw new ArgumentException("Level is null or invalid.");

            if (loops == null || loops.Count == 0)
                throw new ArgumentException("No boundary loops supplied.");

            var footprint = new CurveArray();
            foreach (var c in loops[0])
                footprint.Append(c);
            if (footprint.Size < 3)
                throw new ArgumentException($"Outer boundary has too few curves ({footprint.Size}).");

            log(LogLevel.Info, $"  roofType '{roofType.Name}', level '{level.Name}', outer boundary {footprint.Size} curve(s).");

            FootPrintRoof roof = doc.Create.NewFootPrintRoof(footprint, level, roofType, out ModelCurveArray _);
            if (roof == null)
                throw new InvalidOperationException("NewFootPrintRoof returned null.");

            // Inner room loops become openings; a failed opening never fails the roof.
            for (int i = 1; i < loops.Count; i++)
            {
                try
                {
                    var openingCurves = new CurveArray();
                    foreach (var c in loops[i]) openingCurves.Append(c);
                    doc.Create.NewOpening(roof, openingCurves, true);
                    openingsCut++;
                }
                catch (Exception ex)
                {
                    log(LogLevel.Warning, $"  opening {i} could not be cut: {ex.Message}");
                }
            }

            return roof.Id;
        }
    }
}
