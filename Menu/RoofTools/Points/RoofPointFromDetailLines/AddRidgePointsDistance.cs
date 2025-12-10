using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualBasic; // For InputBox

namespace Revit22_Plugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AddRidgePointsDistance : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uidoc = uiApp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1️⃣ Select the roof
                Reference roofRef = uidoc.Selection.PickObject(ObjectType.Element, new RoofSelectionFilter(), "Select a Roof element");
                RoofBase roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("Add Ridge Points", "❌ Please select a valid Roof element.");
                    return Result.Cancelled;
                }

                // 2️⃣ Select multiple detail lines
                IList<Reference> lineRefs = uidoc.Selection.PickObjects(ObjectType.Element, new DetailLineSelectionFilter(), "Select Detail Lines");
                List<CurveElement> detailLines = lineRefs.Select(r => doc.GetElement(r)).OfType<CurveElement>().ToList();
                if (!detailLines.Any())
                {
                    TaskDialog.Show("Add Ridge Points", "❌ No valid detail lines selected.");
                    return Result.Cancelled;
                }

                // 3️⃣ Ask for interval in mm
                string input = Interaction.InputBox("Enter interval distance (in mm):", "Add Ridge Points", "1000");
                if (!double.TryParse(input, out double intervalMm) || intervalMm <= 0)
                {
                    TaskDialog.Show("Add Ridge Points", "⚠️ Invalid interval input.");
                    return Result.Cancelled;
                }
                double intervalFeet = intervalMm / 304.8;

                // 4️⃣ Enable shape editing
                using (Transaction tx = new Transaction(doc, "Enable Shape Editing"))
                {
                    tx.Start();
                    SlabShapeEditor shapeEditor = roof.GetSlabShapeEditor();
                    if (!shapeEditor.IsEnabled)
                        shapeEditor.Enable();
                    tx.Commit();
                }

                // 5️⃣ Generate evenly spaced points on each detail line
                List<XYZ> allPoints = new List<XYZ>();
                foreach (CurveElement ce in detailLines)
                {
                    Curve curve = ce.GeometryCurve;
                    if (curve == null) continue;

                    double len = curve.Length;
                    int divs = Math.Max(1, (int)(len / intervalFeet));

                    for (int i = 0; i <= divs; i++)
                    {
                        double param = (i * intervalFeet) / len;
                        if (param > 1) param = 1;
                        allPoints.Add(curve.Evaluate(param, true));
                    }
                }

                if (!allPoints.Any())
                {
                    TaskDialog.Show("Add Ridge Points", "⚠️ No valid sample points found on lines.");
                    return Result.Cancelled;
                }

                // 6️⃣ Get roof top face (using our custom service)
                ShapeEditServiceRidge shapeService = new ShapeEditServiceRidge();
                Face topFace = shapeService.GetTopFace(roof);
                if (topFace == null)
                {
                    TaskDialog.Show("Add Ridge Points", "❌ Could not detect roof top face.");
                    return Result.Cancelled;
                }

                // 7️⃣ Project and deduplicate
                double tol = 5.0 / 304.8;
                List<XYZ> projected = new List<XYZ>();
                foreach (XYZ p in allPoints.Distinct(new XYZComparer(tol)))
                {
                    IntersectionResult ir = topFace.Project(p);
                    if (ir != null) projected.Add(ir.XYZPoint);
                }

                if (!projected.Any())
                {
                    TaskDialog.Show("Add Ridge Points", "⚠️ No projected points valid on roof surface.");
                    return Result.Cancelled;
                }

                // 8️⃣ Add shape points (batched safely)
                var (success, fail) = shapeService.EnableAndAddShapePoints(doc, roof, projected);

                TaskDialog.Show("Add Ridge Points", $"✅ Added: {success}\n⚠️ Failed: {fail}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Add Ridge Points – Error", ex.ToString());
                return Result.Failed;
            }
        }
    }

    // Simple comparer for distinct XYZ
    public class XYZComparer : IEqualityComparer<XYZ>
    {
        private readonly double tol;
        public XYZComparer(double tolerance) => tol = tolerance;
        public bool Equals(XYZ a, XYZ b) => a.DistanceTo(b) < tol;
        public int GetHashCode(XYZ obj) => 0;
    }

    // ✅ Unique service (no conflict)
    public class ShapeEditServiceRidge
    {
        public Face GetTopFace(Element elem)
        {
            if (elem == null) throw new ArgumentNullException(nameof(elem));
            GeometryElement geomElem = elem.get_Geometry(new Options());
            if (geomElem == null) return null;

            Face topFace = null;
            double maxZ = double.NegativeInfinity;

            foreach (GeometryObject g in geomElem)
            {
                if (g is Solid solid && solid.Volume > 0)
                {
                    foreach (Face f in solid.Faces)
                    {
                        if (f is PlanarFace pf && pf.FaceNormal.Z > 0 && pf.Origin.Z > maxZ)
                        {
                            maxZ = pf.Origin.Z;
                            topFace = pf;
                        }
                    }
                }
            }
            return topFace;
        }

        public (int success, int fail) EnableAndAddShapePoints(Document doc, RoofBase roof, List<XYZ> pts)
        {
            int success = 0, fail = 0;
            if (doc == null || roof == null || pts == null || !pts.Any()) return (0, 0);

            using (TransactionGroup tg = new TransactionGroup(doc, "Add Ridge Points Group"))
            {
                tg.Start();
                using (Transaction t = new Transaction(doc, "Add Ridge Points"))
                {
                    t.Start();
                    SlabShapeEditor shapeEditor = roof.GetSlabShapeEditor();
                    if (!shapeEditor.IsEnabled)
                        shapeEditor.Enable();

                    int counter = 0;
                    foreach (XYZ p in pts)
                    {
                        try
                        {
                            shapeEditor.AddPoint(p);
                            success++;
                            counter++;
                            // Sub-batch safety for undo stability
                            if (counter % 50 == 0)
                            {
                                t.Commit();
                                t.Start();
                            }
                        }
                        catch { fail++; }
                    }

                    t.Commit();
                }
                tg.Assimilate();
            }

            return (success, fail);
        }
    }
}
