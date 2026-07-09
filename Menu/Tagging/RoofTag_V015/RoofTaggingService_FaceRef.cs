using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTag_V015.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.IO;

namespace Revit26_Plugin.RoofTag_V015
{
    /// <summary>
    /// V015: FaceRef method only.
    /// Places tag on roof face reference (best quality, equivalent to manual click).
    /// Tag WILL DELETE if slab shape is edited (face regenerates).
    /// No fallback — hard fail if FaceRef is invalid.
    /// </summary>
    public static class RoofTaggingService_FaceRef
    {
        private const double DuplicateTolFt = 10.0 / 304.8;

        private static readonly string LogFilePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "RoofTag_V015_Log.txt");

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

            // ── Check for existing tag within 10 mm ──────────────────
            if (DuplicateExistsInView(doc, view, origin))
            {
                var entry = new LogEntry(LogLevel.Warning,
                    $"Skipped (duplicate within 10 mm) — {Fmt(origin)}");
                WriteFile(entry);
                return entry;
            }

            // ── FaceRef only — no fallback ─────────────────────────────
            if (faceRef == null)
                return Fail(origin, "No face reference available.");

            // ── Compute leader geometry ────────────────────────────────
            double bendFt = UnitUtils.ConvertToInternalUnits(vm.BendOffset, UnitTypeId.Millimeters);
            double endFt  = UnitUtils.ConvertToInternalUnits(vm.EndOffset,  UnitTypeId.Millimeters);

            var mode = vm.BendInward
                ? RoofTagGeometryHelper.PlacementMode.Inward
                : RoofTagGeometryHelper.PlacementMode.Outward;

            var (bend, end) = RoofTagGeometryHelper.ComputeTwoStepLeaderPlacement(
                view, roof, origin, bendFt, endFt, mode);

            // ── Try FaceRef ────────────────────────────────────────────
            if (TryPlace(doc, view, faceRef, origin, bend, end, vm))
            {
                var entry = new LogEntry(LogLevel.Success,
                    $"FaceRef — tag placed at {Fmt(origin)}");
                WriteFile(entry);
                return entry;
            }

            // ── Hard fail ──────────────────────────────────────────────
            return Fail(origin, "FaceRef placement failed — no fallback.");
        }

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
                    catch { }
                }
            }
            return false;
        }

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
            catch { }
        }

        private static string Fmt(XYZ p) =>
            $"({p.X:0.000}, {p.Y:0.000}, {p.Z:0.000})";
    }
}
