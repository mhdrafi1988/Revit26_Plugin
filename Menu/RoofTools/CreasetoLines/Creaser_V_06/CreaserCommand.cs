// ============================================================
// File: CreaserCommand.cs
// Namespace: Revit26_Plugin.Creaser_V06.Commands
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.Creaser_V06_01.Commands
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

            if (uiDoc == null || doc == null || view == null)
            {
                message = "Invalid Revit context.";
                return Result.Failed;
            }

            if (view.ViewType != ViewType.FloorPlan &&
                view.ViewType != ViewType.EngineeringPlan &&
                view.ViewType != ViewType.CeilingPlan)
            {
                message = "Run this command in a plan view.";
                return Result.Failed;
            }

            // --------------------------------------------------
            // Select roof
            // --------------------------------------------------
            Reference pickedRef;
            try
            {
                pickedRef = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a roof");
            }
            catch
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
            // Find curve-based detail family
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
            // Extract top roof edges
            // --------------------------------------------------
            Options options = new Options
            {
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geom = roof.get_Geometry(options);
            if (geom == null)
            {
                message = "Failed to read roof geometry.";
                return Result.Failed;
            }

            Plane viewPlane = view.SketchPlane.GetPlane();
            double minLen = doc.Application.ShortCurveTolerance;
            List<Line> lines = new List<Line>();

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid) continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace pf) continue;
                    if (pf.FaceNormal.Z < 0.99) continue;

                    EdgeArray outerLoop = pf.EdgeLoops.get_Item(0);

                    foreach (Edge edge in outerLoop)
                    {
                        Curve c = edge.AsCurve();

                        XYZ p0 = ProjectToPlane(c.GetEndPoint(0), viewPlane);
                        XYZ p1 = ProjectToPlane(c.GetEndPoint(1), viewPlane);

                        if (p0.DistanceTo(p1) < minLen)
                            continue;

                        lines.Add(Line.CreateBound(p0, p1));
                    }
                }
            }

            if (!lines.Any())
            {
                message = "No valid roof edges found.";
                return Result.Failed;
            }

            // --------------------------------------------------
            // Place detail items
            // --------------------------------------------------
            using (Transaction tx =
                new Transaction(doc, "Place Roof Creases"))
            {
                tx.Start();

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }

                foreach (Line line in lines)
                {
                    doc.Create.NewFamilyInstance(
                        line, symbol, view);
                }

                tx.Commit();
            }

            return Result.Succeeded;
        }

        // --------------------------------------------------
        // Helper
        // --------------------------------------------------
        private static XYZ ProjectToPlane(XYZ p, Plane plane)
        {
            XYZ v = p - plane.Origin;
            return p - v.DotProduct(plane.Normal) * plane.Normal;
        }
    }
}
