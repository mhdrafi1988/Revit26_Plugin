using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofRidgePointsCommand2026 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // STEP 1 — Select Roof
                Reference roofRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a Roof element"
                );

                RoofBase roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                    return Fail("Not a roof.");

                // STEP 2 — Extract roof outline
                var outline = new RoofOutlineService().GetRoofOutline(roof, doc);
                if (outline == null || outline.Count == 0)
                    return Fail("Roof outline extraction failed.");

                TaskDialog.Show("Roof Points", $"Roof outline curves: {outline.Count}");

                // STEP 3 — Select detail lines
                IList<Reference> refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new DetailLineSelectionFilter(),
                    "Select Detail Lines"
                );

                List<CurveElement> detailLines =
                    refs.Select(r => doc.GetElement(r)).OfType<CurveElement>().ToList();

                if (!detailLines.Any())
                    return Fail("No detail lines selected.");

                // STEP 4 — Find intersection points
                double tol = 5.0 / 304.8; // 5 mm in feet
                var intersections = new IntersectionService().GetIntersections(outline, detailLines, tol);

                if (!intersections.Any())
                    return Fail("No intersections found.");

                TaskDialog.Show("Roof Points", $"{intersections.Count} intersection points.");

                // STEP 5 — Add slab shape points
                ShapeEditService shapeService = new ShapeEditService();
                using (Transaction t = new Transaction(doc, "Add Shape Points"))
                {
                    t.Start();
                    var (ok, fail) = shapeService.EnableAndAddShapePoints(doc, roof, intersections);
                    t.Commit();

                    TaskDialog.Show("Result", $"Added: {ok}\nFailed: {fail}");
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("ERR", ex.ToString());
                message = ex.Message;
                return Result.Failed;
            }
        }

        private Result Fail(string msg)
        {
            TaskDialog.Show("FAIL", msg);
            return Result.Cancelled;
        }
    }

    // -----------------------------
    // FILTERS
    // -----------------------------

    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element e) => e is RoofBase;
        public bool AllowReference(Reference r, XYZ p) => true;
    }

    public class DetailLineSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element e)
        {
            return e is CurveElement ce &&
                   ce.CurveElementType == CurveElementType.DetailCurve;
        }
        public bool AllowReference(Reference r, XYZ p) => true;
    }

    // -----------------------------
    // ROOF OUTLINE SERVICE
    // -----------------------------

    public class RoofOutlineService
    {
        public List<Curve> GetRoofOutline(RoofBase roof, Document doc)
        {
            List<Curve> curves = new();

            // 1 — Footprint roof loops
            if (roof is FootPrintRoof fpr)
            {
                var profiles = fpr.GetProfiles();
                foreach (CurveArray arr in profiles)
                    foreach (Curve c in arr)
                        curves.Add(c);

                if (curves.Any())
                    return curves;
            }

            // 2 — Solid geometry fallback
            Solid solid = GetRoofSolid(roof);
            if (solid != null)
            {
                foreach (Edge e in solid.Edges)
                    curves.Add(e.AsCurve());

                if (curves.Any())
                    return curves;
            }

            // 3 — Top face edges fallback
            Face topFace = GetTopFace(roof);
            if (topFace != null)
            {
                var loops = topFace.GetEdgesAsCurveLoops();
                var outer = loops.OrderByDescending(l => l.GetExactLength()).FirstOrDefault();
                if (outer != null)
                    curves.AddRange(outer);
            }

            return curves;
        }

        public Solid GetRoofSolid(Element elem)
        {
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine };
            foreach (var obj in elem.get_Geometry(opt))
                if (obj is Solid s && s.Volume > 0)
                    return s;

            return null;
        }

        public Face GetTopFace(Element elem)
        {
            Solid solid = GetRoofSolid(elem);
            if (solid == null) return null;

            Face top = null;
            double maxZ = double.NegativeInfinity;

            foreach (Face f in solid.Faces)
            {
                if (f is PlanarFace pf && pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                {
                    if (pf.Origin.Z > maxZ)
                    {
                        maxZ = pf.Origin.Z;
                        top = pf;
                    }
                }
            }
            return top;
        }
    }

    // -----------------------------
    // INTERSECTION SERVICE
    // -----------------------------

    public class IntersectionService
    {
        public List<XYZ> GetIntersections(List<Curve> roofCurves, List<CurveElement> detailLines, double tolFeet)
        {
            List<XYZ> pts = new();

            foreach (Curve rc in roofCurves)
            {
                foreach (CurveElement ce in detailLines)
                {
                    Curve dl = ce.GeometryCurve;
                    if (dl == null) continue;

                    rc.Intersect(dl, out IntersectionResultArray ira);
                    if (ira == null) continue;

                    foreach (IntersectionResult ir in ira)
                        if (ir.XYZPoint != null)
                            pts.Add(ir.XYZPoint);
                }
            }

            // Deduplicate
            List<XYZ> unique = new();
            foreach (XYZ p in pts)
                if (!unique.Any(u => u.DistanceTo(p) < tolFeet))
                    unique.Add(p);

            return unique;
        }
    }

    // -----------------------------
    // SHAPE EDIT SERVICE
    // -----------------------------

    public class ShapeEditService
    {
        public (int ok, int fail) EnableAndAddShapePoints(Document doc, RoofBase roof, List<XYZ> pts)
        {
            int ok = 0, fail = 0;
            SlabShapeEditor editor;

            try
            {
                editor = roof.GetSlabShapeEditor();
                if (!editor.IsEnabled)
                    editor.Enable();
            }
            catch
            {
                return (0, pts.Count);
            }

            Face topFace = new RoofOutlineService().GetTopFace(roof);
            if (topFace == null)
                return (0, pts.Count);

            foreach (XYZ p in pts)
            {
                try
                {
                    IntersectionResult pr = topFace.Project(p);
                    XYZ final = pr != null ? pr.XYZPoint : p;
                    editor.AddPoint(final);
                    ok++;
                }
                catch
                {
                    fail++;
                }
            }

            return (ok, fail);
        }
    }
}
