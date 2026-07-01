using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTag_V006.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.IO;

namespace Revit26_Plugin.RoofTag_V006
{
    /// <summary>
    /// Places exactly ONE spot elevation tag per point.
    /// Strategy chain:  1) FaceRef  →  2) LevelPlane  →  3) SketchPlane
    /// First success wins; no further strategies are attempted.
    /// OriginOnly (no leader) has been intentionally removed.
    /// </summary>
    public static class RoofTaggingService
    {
        // ── Duplicate proximity tolerance: 10 mm → feet ─────────────────
        private const double DuplicateTolFt = 10.0 / 304.8;

        // ── File log path ────────────────────────────────────────────────
        private static readonly string LogFilePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "RoofTag_V006_Log.txt");

        // ================================================================
        // PUBLIC ENTRY — called once per point from Command
        // Returns a LogEntry describing exactly what happened.
        // ================================================================
        public static LogEntry PlaceTag(
            Document     doc,
            Reference    faceRef,
            RoofBase     roof,
            XYZ          origin,
            RoofTagViewModel vm)
        {
            if (doc == null || roof == null || origin == null || vm == null)
                return Fail(origin, "Null argument passed to PlaceTag.");

            View view = doc.ActiveView;
            if (view == null)
                return Fail(origin, "No active view.");

            // ── 0. Check for existing tag within 10 mm ──────────────────
            if (DuplicateExistsInView(doc, view, origin))
            {
                var entry = new LogEntry(LogLevel.Warning,
                    $"Skipped (duplicate within 10 mm) — {Fmt(origin)}");
                WriteFile(entry);
                return entry;
            }

            // ── Compute leader geometry (same for all strategies) ────────
            double bendFt = UnitUtils.ConvertToInternalUnits(vm.BendOffset, UnitTypeId.Millimeters);
            double endFt  = UnitUtils.ConvertToInternalUnits(vm.EndOffset,  UnitTypeId.Millimeters);

            var mode = vm.BendInward
                ? RoofTagGeometryHelper.PlacementMode.Inward
                : RoofTagGeometryHelper.PlacementMode.Outward;

            var (bend, end) = RoofTagGeometryHelper.ComputeTwoStepLeaderPlacement(
                view, roof, origin, bendFt, endFt, mode);

            // ── 1. FaceRef ───────────────────────────────────────────────
            if (faceRef != null)
            {
                if (TryPlace(doc, view, faceRef, origin, bend, end, vm))
                {
                    var entry = new LogEntry(LogLevel.Success,
                        $"FaceRef — tag placed at {Fmt(origin)}");
                    WriteFile(entry);
                    return entry;
                }
            }

            // ── 2. LevelPlane fallback ───────────────────────────────────
            Reference levelRef = GetLevelPlaneReference(doc, view);
            if (levelRef != null)
            {
                if (TryPlace(doc, view, levelRef, origin, bend, end, vm))
                {
                    var entry = new LogEntry(LogLevel.Warning,
                        $"FaceRef failed → LevelPlane used at {Fmt(origin)}");
                    WriteFile(entry);
                    return entry;
                }
            }

            // ── 3. SketchPlane fallback ──────────────────────────────────
            Reference sketchRef = GetSketchPlaneReference(view);
            if (sketchRef != null)
            {
                if (TryPlace(doc, view, sketchRef, origin, bend, end, vm))
                {
                    var entry = new LogEntry(LogLevel.Warning,
                        $"LevelPlane failed → SketchPlane used at {Fmt(origin)}");
                    WriteFile(entry);
                    return entry;
                }
            }

            // ── All strategies exhausted ─────────────────────────────────
            return Fail(origin, "All strategies failed (FaceRef, LevelPlane, SketchPlane).");
        }

        // ================================================================
        // SINGLE PLACEMENT ATTEMPT
        // ================================================================
        private static bool TryPlace(
            Document         doc,
            View             view,
            Reference        reference,
            XYZ              origin,
            XYZ              bend,
            XYZ              end,
            RoofTagViewModel vm)
        {
            try
            {
                SpotDimension tag = doc.Create.NewSpotElevation(
                    view,
                    reference,
                    origin,
                    bend,
                    end,
                    origin,
                    vm.UseLeader);

                if (tag == null) return false;

                if (vm.SelectedSpotTagType?.TagType != null)
                    tag.ChangeTypeId(vm.SelectedSpotTagType.TagType.Id);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ================================================================
        // DUPLICATE CHECK — scans existing SpotDimension elements in view
        // ================================================================
        private static bool DuplicateExistsInView(Document doc, View view, XYZ origin)
        {
            var existing = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(SpotDimension))
                .WhereElementIsNotElementType();

            foreach (Element el in existing)
            {
                if (el is SpotDimension sd)
                {
                    try
                    {
                        XYZ pos = sd.Origin;
                        if (pos != null && pos.DistanceTo(origin) <= DuplicateTolFt)
                            return true;
                    }
                    catch { /* skip unreadable element */ }
                }
            }
            return false;
        }

        // ================================================================
        // REFERENCE HELPERS
        // ================================================================
        private static Reference GetLevelPlaneReference(Document doc, View view)
        {
            if (view.GenLevel == null) return null;
            try
            {
                Level level = doc.GetElement(view.GenLevel.Id) as Level;
                if (level == null) return null;

                // Use the level element itself as the reference — more reliable
                // than ParseFromStableRepresentation with a Plane.ToString().
                return new Reference(level);
            }
            catch { return null; }
        }

        private static Reference GetSketchPlaneReference(View view)
        {
            if (view.SketchPlane == null) return null;
            try
            {
                return new Reference(view.SketchPlane);
            }
            catch { return null; }
        }

        // ================================================================
        // LOGGING HELPERS
        // ================================================================
        private static LogEntry Fail(XYZ origin, string reason)
        {
            string msg = origin != null
                ? $"ERROR at {Fmt(origin)} — {reason}"
                : $"ERROR — {reason}";

            var entry = new LogEntry(LogLevel.Error, msg);
            WriteFile(entry);
            return entry;
        }

        private static void WriteFile(LogEntry entry)
        {
            try
            {
                File.AppendAllText(
                    LogFilePath,
                    $"{entry}{Environment.NewLine}");
            }
            catch { /* never crash the plugin because of logging */ }
        }

        private static string Fmt(XYZ p) =>
            $"({p.X:0.000}, {p.Y:0.000}, {p.Z:0.000})";
    }
}
