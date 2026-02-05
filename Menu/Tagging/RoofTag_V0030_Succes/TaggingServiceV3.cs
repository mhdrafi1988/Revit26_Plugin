using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using Revit26_Plugin.RoofTag_V03.Helpers;

namespace Revit22_Plugin.RoofTagV3
{
    public static class TaggingServiceV3
    {
        private static readonly string LogFile =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "RoofTagV3_Log.txt");

        // ================================================================
        // Convert UI mm to Revit feet
        // ================================================================
        private static double ConvertMmToFeet(double millimeters)
        {
            return millimeters / 304.8;
        }

        // ================================================================
        // MAIN ENTRY – computes leader geometry + ordered fallback
        // ================================================================
        public static bool PlaceSpotTag(
            Document doc,
            Reference faceRef,
            Element element,
            XYZ origin,
            RoofTagViewModelV3 vm)
        {
            if (doc == null || element == null || origin == null || vm == null)
                return false;

            View view = doc.ActiveView;
            if (view == null)
                return false;

            BoundingBoxXYZ bb = element.get_BoundingBox(view);
            if (bb == null)
                return false;

            // ------------------------------------------------------------
            // Convert UI mm inputs to feet for Revit API
            // ------------------------------------------------------------
            double bendOffsetFt = ConvertMmToFeet(vm.BendOffset);
            double endOffsetFt = ConvertMmToFeet(vm.EndOffset);

            // ------------------------------------------------------------
            // Compute leader bend & end (VIEW SPACE, ORTHOGONAL, SCALE-AWARE)
            // ------------------------------------------------------------
            var placementMode = vm.BendInward
                ? GeometryHelperV3.PlacementMode.Inward
                : GeometryHelperV3.PlacementMode.Outward;

            var (bend, end) = GeometryHelperV3.ComputeTwoStepLeaderPlacement(
                view,
                element,
                origin,
                bendOffsetFt,   // Converted to feet
                endOffsetFt,    // Converted to feet
                placementMode); // Inward / Outward

            // ------------------------------------------------------------
            // 1️⃣ Primary: Face reference
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
            // 4️⃣ Final fallback: Origin-only (no leader geometry)
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

                return Reference.ParseFromStableRepresentation(
                    doc,
                    plane.ToString());
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
                return Reference.ParseFromStableRepresentation(
                    view.Document,
                    plane.ToString());
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