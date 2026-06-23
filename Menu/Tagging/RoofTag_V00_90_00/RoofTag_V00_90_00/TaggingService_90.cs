using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;

namespace Revit22_Plugin.RoofTag_V90
{
    public static class TaggingServiceV3
    {
        private static readonly string LogFile =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "RoofTagV3_Log.txt");

        // ================================================================
        // MAIN ENTRY (multi-strategy placement)
        // ================================================================
        public static bool PlaceSpotTag(
            Document doc,
            Reference faceRef,
            XYZ origin,
            XYZ bend,
            XYZ end,
            RoofTagViewModelV3 vm)
        {
            View view = doc.ActiveView;

            // --- Strategy 1: Use face reference directly ---
            if (faceRef != null)
            {
                if (TryPlace(doc, view, faceRef, origin, bend, end, vm, "FaceRef"))
                    return true;
            }

            // --- Strategy 2: Try Level-based reference ---
            Reference lvlRef = GetLevelPlaneReference(doc, view);
            if (lvlRef != null)
            {
                if (TryPlace(doc, view, lvlRef, origin, bend, end, vm, "LevelPlane"))
                    return true;
            }

            // --- Strategy 3: Try SketchPlane reference ---
            Reference planeRef = GetSketchPlaneReference(view);
            if (planeRef != null)
            {
                if (TryPlace(doc, view, planeRef, origin, bend, end, vm, "SketchPlane"))
                    return true;
            }

            // --- Strategy 4: Last fallback → use faceRef but origin-only ---
            if (faceRef != null)
            {
                if (TryPlace(doc, view, faceRef, origin, origin, origin, vm, "OriginOnly"))
                    return true;
            }

            Log($"[FAIL] Unable to place tag at {PointToString(origin)}");
            return false;
        }

        // ================================================================
        // SINGLE ATTEMPT
        // ================================================================
        private static bool TryPlace(
            Document doc,
            View view,
            Reference reference,
            XYZ origin,
            XYZ bend,
            XYZ end,
            RoofTagViewModelV3 vm,
            string label)
        {
            try
            {
                SpotDimension tag =
                    doc.Create.NewSpotElevation(view, reference, origin, bend, end, origin, vm.UseLeader);

                if (tag != null)
                {
                    tag.ChangeTypeId(vm.SelectedSpotTagType.TagType.Id);
                    Log($"[OK] {label}: Tag placed at {PointToString(origin)}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"[ERR] {label}: FAILED at {PointToString(origin)} | {ex.Message}");
            }
            return false;
        }

        // ================================================================
        // LEVEL REFERENCE (Revit 2022 compatible)
        // ================================================================
        private static Reference GetLevelPlaneReference(Document doc, View view)
        {
            if (view.GenLevel == null) return null;

            try
            {
                Level lvl = doc.GetElement(view.GenLevel.Id) as Level;
                if (lvl == null) return null;

                // Build work plane at level elevation
                Plane p = Plane.CreateByNormalAndOrigin(
                    XYZ.BasisZ,
                    new XYZ(0, 0, lvl.Elevation));

                // Convert Plane to stable rep.
                string stable = p.ToString();
                return Reference.ParseFromStableRepresentation(doc, stable);
            }
            catch
            {
                return null;
            }
        }

        // ================================================================
        // SKETCHPLANE REFERENCE (Revit 2022 compatible)
        // ================================================================
        private static Reference GetSketchPlaneReference(View view)
        {
            if (view.SketchPlane == null) return null;

            try
            {
                Plane p = view.SketchPlane.GetPlane();
                string stable = p.ToString();
                return Reference.ParseFromStableRepresentation(view.Document, stable);
            }
            catch
            {
                return null;
            }
        }

        // ================================================================
        // LOGGING
        // ================================================================
        private static void Log(string text)
        {
            File.AppendAllText(LogFile,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {text}{Environment.NewLine}");
        }

        private static string PointToString(XYZ p)
        {
            return $"({p.X:0.###}, {p.Y:0.###}, {p.Z:0.###})";
        }
    }
}
