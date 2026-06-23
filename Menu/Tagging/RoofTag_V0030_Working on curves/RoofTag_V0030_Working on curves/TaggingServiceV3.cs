using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;

namespace Revit22_Plugin.RoofTagV3
{
    public static class TaggingServiceV3
    {
        private static readonly string LogFile =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "RoofTagV3_Log.txt");

        // ================================================================
        // MAIN ENTRY – per-point ordered fallback
        // ================================================================
        public static bool PlaceSpotTag(
            Document doc,
            Reference faceRef,
            XYZ origin,
            XYZ bend,
            XYZ end,
            RoofTagViewModelV3 vm)
        {
            if (doc == null || origin == null)
                return false;

            View view = doc.ActiveView;
            if (view == null)
                return false;

            // ------------------------------------------------------------
            // 1️⃣ Primary: Face reference (correct binding)
            // ------------------------------------------------------------
            if (faceRef != null)
            {
                if (TryPlace(doc, view, faceRef, origin, bend, end, vm, "FaceRef"))
                    return true;
            }

            // ------------------------------------------------------------
            // 2️⃣ Fallback: Level plane
            // ------------------------------------------------------------
            Reference levelRef = GetLevelPlaneReference(doc, view);
            if (levelRef != null)
            {
                if (TryPlace(doc, view, levelRef, origin, bend, end, vm, "LevelPlane"))
                    return true;
            }

            // ------------------------------------------------------------
            // 3️⃣ Fallback: Sketch plane
            // ------------------------------------------------------------
            Reference sketchRef = GetSketchPlaneReference(view);
            if (sketchRef != null)
            {
                if (TryPlace(doc, view, sketchRef, origin, bend, end, vm, "SketchPlane"))
                    return true;
            }

            // ------------------------------------------------------------
            // 4️⃣ Final fallback: Origin-only (same face ref)
            // ------------------------------------------------------------
            if (faceRef != null)
            {
                if (TryPlace(doc, view, faceRef, origin, origin, origin, vm, "OriginOnly"))
                    return true;
            }

            Log($"[FAIL] Unable to place spot elevation at {PointToString(origin)}");
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
                    doc.Create.NewSpotElevation(
                        view,
                        reference,
                        origin,
                        bend,
                        end,
                        origin,
                        vm.UseLeader);

                if (tag != null)
                {
                    tag.ChangeTypeId(vm.SelectedSpotTagType.TagType.Id);
                    Log($"[OK] {label}: {PointToString(origin)}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"[ERR] {label}: {PointToString(origin)} | {ex.Message}");
            }

            return false;
        }

        // ================================================================
        // LEVEL PLANE REFERENCE
        // ================================================================
        private static Reference GetLevelPlaneReference(Document doc, View view)
        {
            if (view.GenLevel == null)
                return null;

            try
            {
                Level lvl = doc.GetElement(view.GenLevel.Id) as Level;
                if (lvl == null)
                    return null;

                Plane plane = Plane.CreateByNormalAndOrigin(
                    XYZ.BasisZ,
                    new XYZ(0, 0, lvl.Elevation));

                string stable = plane.ToString();
                return Reference.ParseFromStableRepresentation(doc, stable);
            }
            catch
            {
                return null;
            }
        }

        // ================================================================
        // SKETCH PLANE REFERENCE
        // ================================================================
        private static Reference GetSketchPlaneReference(View view)
        {
            if (view.SketchPlane == null)
                return null;

            try
            {
                Plane plane = view.SketchPlane.GetPlane();
                string stable = plane.ToString();
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
            File.AppendAllText(
                LogFile,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {text}{Environment.NewLine}");
        }

        private static string PointToString(XYZ p)
        {
            return $"({p.X:0.###}, {p.Y:0.###}, {p.Z:0.###})";
        }
    }
}
