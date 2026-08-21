using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofFromDetailLines.V007
{
    /// <summary>
    /// Creates a flat FootPrintRoof from a RoofGroup (outer boundary + optional opening
    /// loops). Migrated from the same validated NewFootPrintRoof pattern used across the
    /// plugin suite — every parameter is validated and logged before the API call, since
    /// NewFootPrintRoof has previously thrown ArgumentNullException with no ParamName.
    /// No slope-defining edges are ever set (flat roof only, per confirmed spec).
    /// </summary>
    public static class RoofCreationService
    {
        public static ElementId Create(
            Document doc,
            RoofGroup group,
            ElementId roofTypeId,
            Level level,
            Action<LogLevel, string> log)
        {
            log(LogLevel.Info, $"RoofCreationService: validating parameters for Roof {group.RoofNumber}…");

            if (doc == null || !doc.IsValidObject)
                throw new ArgumentException("Document is null or invalid.");

            if (roofTypeId == null || roofTypeId == ElementId.InvalidElementId)
                throw new ArgumentException($"Invalid roofTypeId: {roofTypeId?.Value}", nameof(roofTypeId));

            var roofType = doc.GetElement(roofTypeId) as RoofType;
            if (roofType == null || !roofType.IsValidObject)
                throw new ArgumentException($"RoofType with ID {roofTypeId.Value} not found or not a RoofType.", nameof(roofTypeId));

            CompoundStructure cs = roofType.GetCompoundStructure();
            log(LogLevel.Info,
                $"  roofType OK: '{roofType.Name}' (id {roofType.Id.Value}), " +
                $"compoundStructure={(cs == null ? "NULL" : $"{cs.LayerCount} layers")}");

            if (cs == null)
                throw new ArgumentException(
                    $"Roof type '{roofType.Name}' has no compound structure (likely Sloped Glazing). Pick a Basic Roof type.");

            if (level == null || !level.IsValidObject)
                throw new ArgumentException("Level is null or invalid.");
            log(LogLevel.Info, $"  level OK: '{level.Name}' (id {level.Id.Value}, el {level.Elevation * 304.8:0} mm)");

            if (group.Outer?.Curves == null || group.Outer.Curves.Count < 2)
                throw new ArgumentException($"Roof {group.RoofNumber}: outer boundary curve list null or too short.");

            var footprint = new CurveArray();
            foreach (var c in group.Outer.Curves)
                footprint.Append(c);
            log(LogLevel.Info, $"  outer boundary: {footprint.Size} curve(s), area {group.Outer.AreaInternal * 0.0929:0.0} m²");

            log(LogLevel.Info, "Calling NewFootPrintRoof…");
            ModelCurveArray mapping = new ModelCurveArray();
            FootPrintRoof roof = doc.Create.NewFootPrintRoof(footprint, level, roofType, out mapping);

            if (roof == null)
                throw new InvalidOperationException("NewFootPrintRoof returned null.");

            log(LogLevel.Info, $"NewFootPrintRoof OK → roof id {roof.Id.Value}, {mapping?.Size ?? 0} model curves mapped");
            log(LogLevel.Info, "Flat roof: no slope-defining edges set (default).");

            // ── Openings: one Opening per nested inner loop, cut into the new roof ──
            foreach (var opening in group.Openings)
            {
                try
                {
                    var openingCurves = new CurveArray();
                    foreach (var c in opening.Curves) openingCurves.Append(c);

                    doc.Create.NewOpening(roof, openingCurves, true);
                    log(LogLevel.Info,
                        $"  opening cut from loop {opening.SerialNumber} — {openingCurves.Size} curve(s), area {opening.AreaInternal * 0.0929:0.0} m²");
                }
                catch (Exception ex)
                {
                    log(LogLevel.Warning, $"  opening from loop {opening.SerialNumber} could not be created: {ex.Message}");
                }
            }

            return roof.Id;
        }
    }
}
