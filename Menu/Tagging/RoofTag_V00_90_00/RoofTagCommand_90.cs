using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.RoofTag_V90
{
    [Transaction(TransactionMode.Manual)]
    public class RoofTagCommandV3 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            RoofBase roof = SelectionHelperV3.SelectRoof(uiDoc);
            if (roof == null)
            {
                TaskDialog.Show("RoofTag V3", "No roof selected.");
                return Result.Cancelled;
            }

            SlabShapeEditor slabShapeEditor = roof.GetSlabShapeEditor();
            if (!slabShapeEditor.IsEnabled)
            {
                using (Transaction tx = new Transaction(doc, "Enable Slab Shape Editor"))
                {
                    tx.Start();
                    slabShapeEditor.Enable();
                    tx.Commit();
                }
            }

            RoofTagWindowV3 window = new RoofTagWindowV3(uiApp);
            bool? result = window.ShowDialog();
            if (result != true)
                return Result.Cancelled;

            RoofTagViewModelV3 vm = (RoofTagViewModelV3)window.DataContext;

            List<XYZ> finalPoints = new List<XYZ>();

            if (vm.UseManualMode)
            {
                TaskDialog.Show("RoofTag V3", "Select roof vertices. Press ESC when done.");
                try
                {
                    IList<Reference> refs = uiDoc.Selection.PickObjects(ObjectType.PointOnElement, "Select points on roof");

                    List<SlabShapeVertex> vertices = slabShapeEditor
                        .SlabShapeVertices.Cast<SlabShapeVertex>().ToList();

                    foreach (Reference r in refs)
                    {
                        XYZ clicked = r.GlobalPoint;

                        SlabShapeVertex nearest = vertices
                            .OrderBy(v => v.Position.DistanceTo(clicked))
                            .FirstOrDefault();

                        if (nearest != null)
                            finalPoints.Add(nearest.Position);
                    }
                }
                catch { }

                if (finalPoints.Count == 0)
                {
                    TaskDialog.Show("RoofTag V3", "No valid points selected.");
                    return Result.Cancelled;
                }
            }
            else
            {
                finalPoints = GeometryHelperV3.GetExactShapeVertices(roof);
                if (finalPoints.Count == 0)
                {
                    TaskDialog.Show("RoofTag V3", "No roof vertices found.");
                    return Result.Cancelled;
                }
            }

            int success = 0;
            int fail = 0;

            View view = uiDoc.ActiveView;
            XYZ centroid = GeometryHelperV3.GetXYCentroid(finalPoints);

            List<XYZ> roofBoundary = GeometryHelperV3.BuildRoofBoundaryXY(roof);

            using (Transaction tx = new Transaction(doc, "Place Roof Tags V3"))
            {
                tx.Start();

                foreach (XYZ pt in finalPoints)
                {
                    XYZ projected;
                    Reference faceRef;
                    GeometryHelperV3.GetTaggingReferenceOnRoof(roof, pt, out projected, out faceRef);

                    if (projected == null || faceRef == null)
                    {
                        fail++;
                        continue;
                    }

                    XYZ bend = GeometryHelperV3.ComputeBendPoint(
                        projected,
                        centroid,
                        vm.BendOffsetFt,
                        vm.BendInward);

                    XYZ end = GeometryHelperV3.ComputeEndPointWithAngle(
                        projected,
                        bend,
                        vm.SelectedAngle,
                        vm.EndOffsetFt,
                        GeometryHelperV3.GetOutwardDirectionForPoint(pt, roofBoundary, centroid),
                        vm.BendInward   // NEW PARAM
                    );

                    end = GeometryHelperV3.AdjustForBoundaryCollisions(bend, end, roofBoundary);

                    bool placed = TaggingServiceV3.PlaceSpotTag(doc, faceRef, projected, bend, end, vm);

                    if (placed) success++;
                    else fail++;
                }

                tx.Commit();
            }

            TaskDialog.Show("RoofTag V3 Summary",
                $"Total: {finalPoints.Count}\n" +
                $"✔ Placed: {success}\n" +
                $"✘ Failed:  {fail}");

            return Result.Succeeded;
        }
    }
}
