using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualBasic; // For InputBox

namespace Revit22_Plugin.RPD.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AddRidgeAllPointsCommand_03 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uidoc = uiApp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1️⃣ Select roof
                Reference roofRef = uidoc.Selection.PickObject(ObjectType.Element, new RoofSelectionFilter(), "Select a Roof element");
                RoofBase roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("Add Ridge Points", "❌ Please select a valid Roof element.");
                    return Result.Cancelled;
                }

                // 2️⃣ Select detail lines
                IList<Reference> lineRefs = uidoc.Selection.PickObjects(ObjectType.Element, new DetailLineSelectionFilter(), "Select Detail Lines");
                List<CurveElement> detailLines = lineRefs.Select(r => doc.GetElement(r)).OfType<CurveElement>().ToList();
                if (detailLines.Count == 0)
                {
                    TaskDialog.Show("Add Ridge Points", "❌ No valid detail lines selected.");
                    return Result.Cancelled;
                }

                // 3️⃣ Get interval input
                string input = Interaction.InputBox("Enter interval distance (in mm):", "Add Ridge Points", "1000");
                if (!double.TryParse(input, out double intervalMm) || intervalMm <= 0)
                {
                    TaskDialog.Show("Add Ridge Points", "⚠️ Invalid interval input.");
                    return Result.Cancelled;
                }
                double intervalFeet = intervalMm / 304.8;
                double tolFeet = 5.0 / 304.8;

                // 4️⃣ Enable shape editing
                using (Transaction tx = new Transaction(doc, "Enable Shape Editing"))
                {
                    tx.Start();
                    SlabShapeEditor shapeEditor = roof.GetSlabShapeEditor();
                    if (!shapeEditor.IsEnabled)
                        shapeEditor .Enable();
                    tx.Commit();
                }

                // 5️⃣ Get roof boundaries (outer + inner openings)
                RoofOutlineService outlineService = new RoofOutlineService();
                var (outerCurves, innerOpenings) = outlineService.GetRoofBoundaries(roof);
                if (outerCurves == null || outerCurves.Count == 0)
                {
                    TaskDialog.Show("Add Ridge Points", "❌ Could not extract roof boundaries.");
                    return Result.Cancelled;
                }

                // 6️⃣ Collect all points
                IntersectionService intService = new IntersectionService();
                List<XYZ> allPoints = new List<XYZ>();

                // Ask whether to include roof outline points
                TaskDialogResult includeOutline = TaskDialog.Show(
                    "Outline Option",
                    "Do you want to add points along the roof's outer and inner outlines as well?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                bool addOutlinePoints = includeOutline == TaskDialogResult.Yes;

                // A. Border + Interval points on detail lines
                foreach (CurveElement ce in detailLines)
                {
                    Curve c = ce.GeometryCurve;
                    if (c == null) continue;
                    allPoints.Add(c.GetEndPoint(0));
                    allPoints.Add(c.GetEndPoint(1));

                    double length = c.Length;
                    int divisions = Math.Max(1, (int)(length / intervalFeet));
                    for (int i = 1; i < divisions; i++)
                    {
                        double param = i * intervalFeet / length;
                        if (param > 1) param = 1;
                        allPoints.Add(c.Evaluate(param, true));
                    }
                }

                // B. Intersections with roof outer boundary
                List<XYZ> roofIntersections = intService.GetIntersections(outerCurves, detailLines, tolFeet);
                allPoints.AddRange(roofIntersections);

                // C. Intersections with roof inner openings
                foreach (var opening in innerOpenings)
                {
                    var openingIntersections = intService.GetIntersections(opening, detailLines, tolFeet);
                    allPoints.AddRange(openingIntersections);
                }

                // D. Intersections between detail lines
                List<XYZ> lineIntersections = intService.GetIntersectionsBetweenDetailLines(detailLines, tolFeet);
                allPoints.AddRange(lineIntersections);

                // E. (Optional) Add points on roof outlines (outer + inner)
                if (addOutlinePoints)
                {
                    void AddCurvePoints(List<Curve> curves)
                    {
                        foreach (Curve c in curves)
                        {
                            if (c == null) continue;
                            allPoints.Add(c.GetEndPoint(0));
                            allPoints.Add(c.GetEndPoint(1));

                            double len = c.Length;
                            int divs = Math.Max(1, (int)(len / intervalFeet));
                            for (int i = 1; i < divs; i++)
                            {
                                double param = i * intervalFeet / len;
                                if (param > 1) param = 1;
                                allPoints.Add(c.Evaluate(param, true));
                            }
                        }
                    }
                    AddCurvePoints(outerCurves);
                    foreach (var innerLoop in innerOpenings)
                        AddCurvePoints(innerLoop);
                }

                // Deduplicate
                allPoints = allPoints.Distinct(new XYZComparer(tolFeet)).ToList();
                if (allPoints.Count == 0)
                {
                    TaskDialog.Show("Add Ridge Points", "⚠️ No valid points to process.");
                    return Result.Cancelled;
                }

                // 7️⃣ Project to roof top face
                ShapeEditServiceRidge shapeService = new ShapeEditServiceRidge();
                Face topFace = shapeService.GetTopFace(roof);
                if (topFace == null)
                {
                    TaskDialog.Show("Add Ridge Points", "❌ Could not detect roof top face.");
                    return Result.Cancelled;
                }

                List<XYZ> projected = new List<XYZ>();
                foreach (XYZ p in allPoints)
                {
                    IntersectionResult ir = topFace.Project(p);
                    if (ir != null) projected.Add(ir.XYZPoint);
                }
                if (projected.Count == 0)
                {
                    TaskDialog.Show("Add Ridge Points", "⚠️ No projected points landed on roof surface.");
                    return Result.Cancelled;
                }

                // 8️⃣ Add shape points
                var (success, fail) = shapeService.EnableAndAddShapePoints(doc, roof, projected);
                TaskDialog.Show("Add Ridge Points",
                    $"✅ Total Points Added: {success}\n⚠️ Failed: {fail}\nTotal Attempted: {projected.Count}\n" +
                    $"Inner Openings: {innerOpenings.Count}\nOutline Points Added: {(addOutlinePoints ? "Yes" : "No")}");
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

    // --- Filters ---
    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element e) => e is RoofBase;
        public bool AllowReference(Reference r, XYZ p) => true;
    }

    public class DetailLineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element e) => e is CurveElement ce && ce.CurveElementType == CurveElementType.DetailCurve;
        public bool AllowReference(Reference r, XYZ p) => true;
    }

    // --- XYZ comparer ---
    public class XYZComparer : IEqualityComparer<XYZ>
    {
        private readonly double tol;
        public XYZComparer(double tolerance) => tol = tolerance;
        public bool Equals(XYZ a, XYZ b) => a.DistanceTo(b) < tol;
        public int GetHashCode(XYZ obj) => 0;
    }

    // --- Intersection logic (flattened to XY plane) ---
    public class IntersectionService
    {
        public List<XYZ> GetIntersections(List<Curve> roofCurves, List<CurveElement> detailLines, double tolFeet)
        {
            List<XYZ> pts = new List<XYZ>();

            foreach (Curve rc in roofCurves)
            {
                Curve flatRoof = FlattenCurve(rc);
                foreach (CurveElement ce in detailLines)
                {
                    Curve dl = ce.GeometryCurve;
                    if (dl == null) continue;
                    Curve flatLine = FlattenCurve(dl);

                    flatRoof.Intersect(flatLine, out IntersectionResultArray ira);
                    if (ira == null) continue;

                    foreach (IntersectionResult ir in ira)
                    {
                        if (ir.XYZPoint == null) continue;
                        XYZ flatPoint = ir.XYZPoint;
                        IntersectionResult proj = rc.Project(flatPoint);
                        if (proj != null) pts.Add(proj.XYZPoint);
                    }
                }
            }
            return pts.Distinct(new XYZComparer(tolFeet)).ToList();
        }

        public List<XYZ> GetIntersectionsBetweenDetailLines(List<CurveElement> lines, double tolFeet)
        {
            List<XYZ> pts = new List<XYZ>();
            for (int i = 0; i < lines.Count; i++)
            {
                Curve c1 = FlattenCurve(lines[i].GeometryCurve);
                if (c1 == null) continue;
                for (int j = i + 1; j < lines.Count; j++)
                {
                    Curve c2 = FlattenCurve(lines[j].GeometryCurve);
                    if (c2 == null) continue;
                    c1.Intersect(c2, out IntersectionResultArray ira);
                    if (ira == null) continue;

                    foreach (IntersectionResult ir in ira)
                        if (ir.XYZPoint != null)
                            pts.Add(ir.XYZPoint);
                }
            }
            return pts.Distinct(new XYZComparer(tolFeet)).ToList();
        }

        private static Curve FlattenCurve(Curve c)
        {
            if (c == null) return null;
            XYZ p0 = new XYZ(c.GetEndPoint(0).X, c.GetEndPoint(0).Y, 0);
            XYZ p1 = new XYZ(c.GetEndPoint(1).X, c.GetEndPoint(1).Y, 0);

            if (c is Arc arc)
            {
                XYZ mid = arc.Evaluate(0.5, true);
                mid = new XYZ(mid.X, mid.Y, 0);
                return Arc.Create(p0, p1, mid);
            }
            return Line.CreateBound(p0, p1);
        }
    }

    // --- Roof outline logic ---
    public class RoofOutlineService
    {
        public (List<Curve> Outer, List<List<Curve>> Inner) GetRoofBoundaries(RoofBase roof)
        {
            List<Curve> outer = new List<Curve>();
            List<List<Curve>> inner = new List<List<Curve>>();

            Face topFace = GetTopFace(roof);
            if (topFace == null) return (outer, inner);

            var loops = topFace.GetEdgesAsCurveLoops();
            if (loops == null || loops.Count == 0) return (outer, inner);

            var ordered = loops.OrderByDescending(l => l.GetExactLength()).ToList();
            outer.AddRange(ordered.FirstOrDefault() ?? new CurveLoop());

            foreach (var loop in ordered.Skip(1))
            {
                List<Curve> innerLoop = new List<Curve>();
                foreach (Curve c in loop)
                    if (c != null) innerLoop.Add(c);
                if (innerLoop.Count > 0)
                    inner.Add(innerLoop);
            }
            return (outer, inner);
        }

        private Solid GetRoofSolid(Element elem)
        {
            Options opt = new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geomElem = elem.get_Geometry(opt);
            foreach (GeometryObject go in geomElem)
                if (go is Solid s && s.Volume > 0)
                    return s;
            return null;
        }

        private Face GetTopFace(Element elem)
        {
            Solid solid = GetRoofSolid(elem);
            if (solid == null) return null;

            Face top = null;
            double maxZ = double.NegativeInfinity;
            foreach (Face f in solid.Faces)
            {
                if (f is PlanarFace pf && pf.FaceNormal.Z > 0 && pf.Origin.Z > maxZ)
                {
                    maxZ = pf.Origin.Z;
                    top = pf;
                }
            }
            return top;
        }
    }

    // --- Shape editing service ---
    public class ShapeEditServiceRidge
    {
        public Face GetTopFace(Element elem)
        {
            Options opt = new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geomElem = elem.get_Geometry(opt);
            Face top = null;
            double maxZ = double.NegativeInfinity;
            foreach (GeometryObject go in geomElem)
            {
                if (go is Solid s && s.Volume > 0)
                {
                    foreach (Face f in s.Faces)
                    {
                        if (f is PlanarFace pf && pf.FaceNormal.Z > 0 && pf.Origin.Z > maxZ)
                        {
                            maxZ = pf.Origin.Z;
                            top = pf;
                        }
                    }
                }
            }
            return top;
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

                    int count = 0;
                    foreach (XYZ p in pts)
                    {
                        try
                        {
                            shapeEditor.AddPoint(p);
                            success++;
                            count++;
                            if (count % 50 == 0)
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
