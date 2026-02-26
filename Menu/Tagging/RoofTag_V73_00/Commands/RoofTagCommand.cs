using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
//using Revit22_Plugin.RoofTagV3;
using Revit26_Plugin.RoofTag_V73.Helpers;
using Revit26_Plugin.RoofTag_V73.Services;
using Revit26_Plugin.RoofTag_V73.ViewModels;
using Revit26_Plugin.RoofTag_V73.Views;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTag_V73.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RoofTagCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            RoofBase roof = SelectionHelper.SelectRoof(uiDoc);
            if (roof == null)
            {
                TaskDialog.Show("RoofTag", "No roof selected.");
                return Result.Cancelled;
            }

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (!editor.IsEnabled)
            {
                using Transaction tx = new(doc, "Enable Slab Shape Editor");
                tx.Start();
                editor.Enable();
                tx.Commit();
            }

            RoofTagWindow window = new(uiApp);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            RoofTagViewModel vm = (RoofTagViewModel)window.DataContext;

            List<XYZ> points = vm.UseManualMode
                ? uiDoc.Selection
                    .PickObjects(ObjectType.PointOnElement, "Select points on roof")
                    .Select(r => r.GlobalPoint)
                    .ToList()
                : GeometryHelper.GetExactShapeVertices(roof);

            if (points.Count == 0)
            {
                TaskDialog.Show("RoofTag", "No valid points found.");
                return Result.Cancelled;
            }

            XYZ centroid = GetXYCentroid(points);
            List<XYZ> boundary = GeometryHelper.BuildRoofBoundaryXY(roof);

            int success = 0;
            int fail = 0;

            using Transaction txPlace = new(doc, "Place Roof Tags");
            txPlace.Start();

            foreach (XYZ pt in points)
            {
                if (!GeometryHelper.GetTaggingReferenceOnRoof(
                    roof, pt, out Reference faceRef, out XYZ projected))
                {
                    fail++;
                    continue;
                }

                XYZ bend = GeometryHelper.ComputeBendPoint(
                    projected, centroid, vm.BendOffsetFt, vm.BendInward);

                XYZ end = GeometryHelper.ComputeEndPointWithAngle(
                    projected,
                    bend,
                    vm.SelectedAngle,
                    vm.EndOffsetFt,
                    GeometryHelper.GetOutwardDirectionForPoint(pt, boundary, centroid),
                    vm.BendInward);

                end = GeometryHelper.AdjustForBoundaryCollisions(bend, end, boundary);

                if (TaggingService.PlaceSpotTag(doc, faceRef, projected, bend, end, vm))
                    success++;
                else
                    fail++;
            }

            txPlace.Commit();

            TaskDialog.Show("RoofTag", $"Placed: {success}\nFailed: {fail}");
            return Result.Succeeded;
        }

        private static XYZ GetXYCentroid(List<XYZ> points)
        {
            double x = points.Sum(p => p.X);
            double y = points.Sum(p => p.Y);
            return new XYZ(x / points.Count, y / points.Count, 0);
        }
    }
}
