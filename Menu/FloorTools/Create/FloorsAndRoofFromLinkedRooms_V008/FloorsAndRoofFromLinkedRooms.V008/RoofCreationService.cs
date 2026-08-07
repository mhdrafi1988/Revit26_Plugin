using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V008
{
    /// <summary>
    /// V008 — Migrated from FootprintRoofFromLines V001's proven RoofCreationService,
    /// fixing V007's roof-creation failures.
    /// Validates every parameter, builds the CurveArray, and calls
    /// Document.Create.NewFootPrintRoof (Revit 2026 — current API, no newer
    /// replacement). Logs each parameter and step so a failure pinpoints
    /// the exact stage — required because NewFootPrintRoof has previously
    /// thrown ArgumentNullException with no ParamName inside external events.
    /// Signature kept V007-native: IList&lt;Curve&gt; + ElementId roofTypeId
    /// (resolved to RoofType internally), plus a log callback.
    /// </summary>
    public static class RoofCreationService
    {
        public static ElementId Create(
            Document doc,
            IList<Curve> orderedCurves,
            ElementId roofTypeId,
            Level level,
            Action<LogLevel, string> log)
        {
            // ── Parameter validation — every input, before any API create call ──
            log(LogLevel.Info, "RoofCreationService: validating parameters…");

            if (doc == null || !doc.IsValidObject)
                throw new ArgumentException("Document is null or invalid.");
            log(LogLevel.Info, $"  doc OK: '{doc.Title}'");

            if (roofTypeId == null || roofTypeId == ElementId.InvalidElementId)
                throw new ArgumentException($"Invalid roofTypeId: {roofTypeId?.Value}", nameof(roofTypeId));

            var roofType = doc.GetElement(roofTypeId) as RoofType;
            if (roofType == null || !roofType.IsValidObject)
                throw new ArgumentException($"RoofType with ID {roofTypeId.Value} not found or not a RoofType.", nameof(roofTypeId));

            // Log the type's family + compound-structure state. A roof type with a
            // null CompoundStructure (e.g. Sloped Glazing) makes NewFootPrintRoof
            // throw ArgumentNullException with no ParamName — the recurring crash.
            CompoundStructure cs = roofType.GetCompoundStructure();
            string familyName = roofType.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME)?.AsValueString()
                                ?? "(unknown family)";
            log(LogLevel.Info,
                $"  roofType OK: '{roofType.Name}' (id {roofType.Id.Value}), family '{familyName}', " +
                $"compoundStructure={(cs == null ? "NULL" : $"{cs.LayerCount} layers")}");

            if (cs == null)
                throw new ArgumentException(
                    $"Roof type '{roofType.Name}' has no compound structure (likely Sloped Glazing). " +
                    "Pick a Basic Roof type.");

            if (level == null || !level.IsValidObject)
                throw new ArgumentException("Level is null or invalid.");
            log(LogLevel.Info, $"  level OK: '{level.Name}' (id {level.Id.Value}, el {level.Elevation * 304.8:0} mm)");

            if (orderedCurves == null || orderedCurves.Count < 2)
                throw new ArgumentException($"Curve list null or too short ({orderedCurves?.Count ?? 0}).");

            var footprint = new CurveArray();
            for (int i = 0; i < orderedCurves.Count; i++)
            {
                Curve c = orderedCurves[i];
                if (c == null)
                    throw new ArgumentException($"Curve {i + 1} is null.");
                if (!c.IsBound)
                    throw new ArgumentException($"Curve {i + 1} is unbound.");
                XYZ p0 = c.GetEndPoint(0), p1 = c.GetEndPoint(1);
                log(LogLevel.Info,
                    $"  curve {i + 1}/{orderedCurves.Count}: {c.GetType().Name} " +
                    $"({p0.X * 304.8:0},{p0.Y * 304.8:0}) → ({p1.X * 304.8:0},{p1.Y * 304.8:0}) mm, " +
                    $"len {c.Length * 304.8:0} mm");
                footprint.Append(c);
            }
            log(LogLevel.Info, $"  CurveArray built: {footprint.Size} curves");

            // ── Create ─────────────────────────────────────────────────────
            ModelCurveArray mapping = new ModelCurveArray();

            log(LogLevel.Info, "Calling NewFootPrintRoof…");
            FootPrintRoof roof = doc.Create.NewFootPrintRoof(
                footprint, level, roofType, out mapping);

            if (roof == null)
                throw new InvalidOperationException("NewFootPrintRoof returned null.");

            log(LogLevel.Info,
                $"NewFootPrintRoof OK → roof id {roof.Id.Value}, {mapping?.Size ?? 0} model curves mapped");
            log(LogLevel.Info, "Flat roof: no slope-defining edges set (default)");

            return roof.Id;
        }
    }
}