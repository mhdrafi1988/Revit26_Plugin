// ============================================================
// File: SelectRoofAndPlaceEdgeDetailsCommand.cs
// Namespace: Revit26_Plugin.Creaser_V05.Commands
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.Creaser_V06.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreaserCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc?.Document;
            View view = doc?.ActiveView;

            // --------------------------------------------------
            // 1. Validate context
            // --------------------------------------------------
            if (uiDoc == null || doc == null || view == null)
            {
                message = "Invalid Revit context.";
                return Result.Failed;
            }

            // --------------------------------------------------
            // 2. Validate plan view
            // --------------------------------------------------
            if (view.ViewType != ViewType.FloorPlan &&
                view.ViewType != ViewType.EngineeringPlan &&
                view.ViewType != ViewType.CeilingPlan)
            {
                message = "Command must be run in a Plan View.";
                return Result.Failed;
            }

            // --------------------------------------------------
            // 3. Pick roof only
            // --------------------------------------------------
            Reference pickedRef;
            try
            {
                pickedRef = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a roof");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            RoofBase roof = doc.GetElement(pickedRef) as RoofBase;
            if (roof == null)
            {
                message = "Selected element is not a roof.";
                return Result.Failed;
            }

            // --------------------------------------------------
            // 4. Find curve-based detail family
            // --------------------------------------------------
            FamilySymbol symbol =
                new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_DetailComponents)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s =>
                        s.Family.FamilyPlacementType ==
                        FamilyPlacementType.CurveBasedDetail);

            if (symbol == null)
            {
                message = "No curve-based detail family found.";
                return Result.Failed;
            }

            // --------------------------------------------------
            // 5. Extract roof top edges
            // --------------------------------------------------
            Options options = new Options
            {
                DetailLevel = ViewDetailLevel.Fine,
                ComputeReferences = false
            };

            GeometryElement geom = roof.get_Geometry(options);
            if (geom == null)
            {
                message = "Failed to read roof geometry.";
                return Result.Failed;
            }

            Plane viewPlane = view.SketchPlane.GetPlane();
            double minCurveLen = doc.Application.ShortCurveTolerance;

            List<Line> projectedLines = new List<Line>();

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid || solid.Faces.Size == 0)
                    continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace pf)
                        continue;

                    // Only top horizontal faces
                    if (pf.FaceNormal.Z < 0.99)
                        continue;

                    // Outer loop only (index 0)
                    EdgeArray outerLoop = pf.EdgeLoops.get_Item(0);

                    foreach (Edge edge in outerLoop)
                    {
                        Curve c = edge.AsCurve();

                        XYZ p0 = ProjectToPlane(c.GetEndPoint(0), viewPlane);
                        XYZ p1 = ProjectToPlane(c.GetEndPoint(1), viewPlane);

                        if (p0.DistanceTo(p1) < minCurveLen)
                            continue;

                        projectedLines.Add(Line.CreateBound(p0, p1));
                    }
                }
            }

            if (!projectedLines.Any())
            {
                message = "No valid roof edges found.";
                return Result.Failed;
            }

            // --------------------------------------------------
            // 6. Place detail items
            // --------------------------------------------------
            using (Transaction tx =
                new Transaction(doc, "Place Roof Edge Details"))
            {
                tx.Start();

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }

                foreach (Line line in projectedLines)
                {
                    doc.Create.NewFamilyInstance(
                        line,
                        symbol,
                        view);
                }

                tx.Commit();
            }

            TaskDialog.Show(
                "Success",
                $"Roof edges processed.\nDetail items placed: {projectedLines.Count}\nRoof Id: {roof.Id.Value}");

            return Result.Succeeded;
        }

        // --------------------------------------------------
        // Helpers
        // --------------------------------------------------
        private static XYZ ProjectToPlane(XYZ p, Plane plane)
        {
            XYZ v = p - plane.Origin;
            return p - v.DotProduct(plane.Normal) * plane.Normal;
        }
    }

    // ============================================================
    // Roof-only selection filter
    // ============================================================
    internal class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is RoofBase;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
