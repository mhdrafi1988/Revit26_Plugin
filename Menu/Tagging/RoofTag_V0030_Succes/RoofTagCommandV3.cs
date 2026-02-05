using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit22_Plugin.RoofTagV3;
using Revit26_Plugin.RoofTag_V03.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTag_V03.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RoofTagCommandV3 : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View activeView = doc.ActiveView;

            RoofBase roof = SelectionHelperV3.SelectRoof(uiDoc);
            if (roof == null)
            {
                TaskDialog.Show("RoofTag V3", "No roof selected.");
                return Result.Cancelled;
            }

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (!editor.IsEnabled)
            {
                using (Transaction tx = new Transaction(doc, "Enable Slab Shape Editor"))
                {
                    tx.Start();
                    editor.Enable();
                    tx.Commit();
                }
            }

            RoofTagWindowV3 window = new RoofTagWindowV3(uiApp);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            RoofTagViewModelV3 vm = (RoofTagViewModelV3)window.DataContext;

            List<XYZ> points;

            if (vm.UseManualMode)
            {
                IList<Reference> refs =
                    uiDoc.Selection.PickObjects(ObjectType.PointOnElement, "Select points on roof");

                points = refs.Select(r => r.GlobalPoint).ToList();
            }
            else
            {
                points = GeometryHelperV3.GetExactShapeVertices(roof);
            }

            if (points.Count == 0)
            {
                TaskDialog.Show("RoofTag V3", "No valid points found.");
                return Result.Cancelled;
            }

            // Get bounding box of points
            XYZ centroid = GetXYCentroid(points);
            List<XYZ> boundary = GeometryHelperV3.BuildRoofBoundaryXY(roof);

            // Determine placement mode from view model
            GeometryHelperV3.PlacementMode placementMode = vm.BendInward ?
                GeometryHelperV3.PlacementMode.Inward :
                GeometryHelperV3.PlacementMode.Outward;

            int success = 0;
            int fail = 0;

            using (Transaction tx = new Transaction(doc, "Place Roof Tags V3"))
            {
                tx.Start();

                foreach (XYZ pt in points)
                {
                    bool ok = GeometryHelperV3.GetTaggingReferenceOnRoof(
                        roof,
                        pt,
                        out Reference faceRef,
                        out XYZ projected);

                    if (!ok)
                    {
                        fail++;
                        continue;
                    }

                    // Convert UI mm to feet for geometry calculations
                    double bendOffsetFt = UnitUtils.ConvertToInternalUnits(vm.BendOffset, UnitTypeId.Millimeters);
                    double endOffsetFt = UnitUtils.ConvertToInternalUnits(vm.EndOffset, UnitTypeId.Millimeters);

                    // OPTION 1: Use the new view-based two-step leader calculation
                    // This uses view-space logic with bounding box
                    (XYZ bend, XYZ end) = GeometryHelperV3.ComputeTwoStepLeaderPlacement(
                        activeView,
                        roof,
                        projected,
                        bendOffsetFt,  // Converted to feet
                        endOffsetFt,   // Converted to feet
                        placementMode);

                    // OPTION 2: Use legacy centroid-based calculation (alternative)
                    // Uncomment below and comment above if you prefer centroid-based
                    /*
                    XYZ bend = GeometryHelperV3.ComputeBendPoint(
                        projected,
                        centroid,
                        bendOffsetFt,
                        vm.BendInward);

                    XYZ end = GeometryHelperV3.ComputeEndPointWithAngle(
                        projected,
                        bend,
                        vm.SelectedAngle,
                        endOffsetFt,
                        GeometryHelperV3.GetOutwardDirectionForPoint(pt, boundary, centroid),
                        vm.BendInward);
                    */

                    // Apply boundary collision adjustment if needed
                    end = GeometryHelperV3.AdjustForBoundaryCollisions(bend, end, boundary);

                    if (TaggingServiceV3.PlaceSpotTag(doc, faceRef, roof, projected, vm))
                        success++;
                    else
                        fail++;
                }

                tx.Commit();
            }

            TaskDialog.Show("RoofTag V3",
                $"Placed: {success}\nFailed: {fail}");

            return Result.Succeeded;
        }

        private static XYZ GetXYCentroid(List<XYZ> points)
        {
            if (points == null || points.Count == 0)
                return XYZ.Zero;

            double sumX = 0, sumY = 0;
            foreach (var pt in points)
            {
                sumX += pt.X;
                sumY += pt.Y;
            }
            double count = points.Count;
            return new XYZ(sumX / count, sumY / count, 0);
        }
    }
}