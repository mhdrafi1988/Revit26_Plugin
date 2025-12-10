// ✅ COMBINED AND VALIDATED VERSION
// Revit 2022 Plugin – Roof Shape Points Inserter

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofRidgePointsCommand04 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uidoc = uiApp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // Step 1 – Select a roof
                Reference roofRef = uidoc.Selection.PickObject(ObjectType.Element, new RoofSelectionFilter(), "Select a Roof element");
                RoofBase roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("Roof Shape Points", "❌ Invalid selection – only Roof elements are supported.");
                    return Result.Cancelled;
                }

                // Step 2 – Get roof outline
                RoofOutlineService outlineService = new RoofOutlineService();
                var curves = outlineService.GetRoofOutline(roof, doc);
                if (curves == null || curves.Count == 0)
                {
                    TaskDialog.Show("Roof Shape Points", "❌ Failed to extract roof outline.");
                    return Result.Cancelled;
                }
                TaskDialog.Show("Roof Shape Points", $"✅ Roof outline found ({curves.Count} curves). Select detail lines.");

                // Step 3 – Select detail lines
                IList<Reference> lineRefs = uidoc.Selection.PickObjects(ObjectType.Element, new DetailLineSelectionFilter(), "Select Detail Lines");
                List<CurveElement> detailLines = lineRefs.Select(r => doc.GetElement(r)).OfType<CurveElement>().ToList();
                if (!detailLines.Any())
                {
                    TaskDialog.Show("Roof Shape Points", "❌ No detail lines selected.");
                    return Result.Cancelled;
                }

                // Step 4 – Find intersections
                IntersectionService intService = new IntersectionService();
                List<XYZ> intersections = intService.GetIntersections(curves, detailLines, 5.0 / 304.8);
                if (intersections.Count == 0)
                {
                    TaskDialog.Show("Roof Shape Points", "⚠️ No intersections found between roof and lines.");
                    return Result.Cancelled;
                }
                TaskDialog.Show("Roof Shape Points", $"✅ {intersections.Count} unique intersection points found.");

                // Step 5 – Add shape points
                ShapeEditService shapeService = new ShapeEditService();
                using (TransactionGroup tg = new TransactionGroup(doc, "Roof Shape Points"))
                {
                    tg.Start();
                    using (Transaction t = new Transaction(doc, "Add Points"))
                    {
                        t.Start();
                        (int success, int fail) = shapeService.EnableAndAddShapePoints(doc, roof, intersections);
                        if (success == 0)
                        {
                            t.RollBack();
                            tg.RollBack();
                            TaskDialog.Show("Roof Shape Points", "❌ No valid points added – rolled back.");
                            return Result.Failed;
                        }
                        t.Commit();
                        TaskDialog.Show("Roof Shape Points", $"✅ Points added: {success}\n⚠️ Failed: {fail}");
                    }
                    tg.Assimilate();
                }
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Roof Shape Points – Error", ex.ToString());
                return Result.Failed;
            }
        }
    }

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

    public class RoofOutlineService
    {
        public List<Curve> GetRoofOutline(RoofBase roof, Document doc)
        {
            List<Curve> curves = new List<Curve>();

            // 1️⃣ Try footprint
            if (roof is FootPrintRoof fpr)
            {
                var profiles = fpr.GetProfiles();
                if (profiles != null)
                {
                    foreach (CurveArray ca in profiles)
                        foreach (Curve c in ca)
                            if (c != null) curves.Add(c);
                }
                if (curves.Any()) return curves;
            }

            // 2️⃣ Fallback – solid edges
            Solid roofSolid = GetRoofSolid(roof);
            if (roofSolid != null)
            {
                foreach (Edge e in roofSolid.Edges)
                {
                    Curve c = e.AsCurve();
                    if (c != null) curves.Add(c);
                }
                if (curves.Any()) return curves;
            }

            // 3️⃣ Fallback – top face loop
            Face topFace = GetTopFace(roof);
            if (topFace != null)
            {
                var loops = topFace.GetEdgesAsCurveLoops();
                var outer = loops.OrderByDescending(l => l.GetExactLength()).FirstOrDefault();
                if (outer != null) curves.AddRange(outer);
            }

            return curves;
        }

        private Solid GetRoofSolid(Element elem)
        {
            Options opt = new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geomElem = elem.get_Geometry(opt);
            foreach (GeometryObject go in geomElem)
                if (go is Solid s && s.Volume > 0) return s;
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

    public class IntersectionService
    {
        public List<XYZ> GetIntersections(List<Curve> roofCurves, List<CurveElement> detailLines, double tolFeet)
        {
            List<XYZ> points = new List<XYZ>();

            foreach (Curve rc in roofCurves)
            {
                foreach (CurveElement ce in detailLines)
                {
                    Curve dl = ce.GeometryCurve;
                    if (dl == null) continue;
                    rc.Intersect(dl, out IntersectionResultArray ira);
                    if (ira != null)
                    {
                        foreach (IntersectionResult i in ira)
                            if (i.XYZPoint != null)
                                points.Add(i.XYZPoint);
                    }
                }
            }

            // Deduplicate by distance tolerance
            List<XYZ> unique = new List<XYZ>();
            foreach (XYZ p in points)
            {
                bool exists = unique.Any(u => u.DistanceTo(p) < tolFeet);
                if (!exists) unique.Add(p);
            }
            return unique;
        }
    }

    public class ShapeEditService
    {
        public (int success, int fail) EnableAndAddShapePoints(Document doc, RoofBase roof, List<XYZ> pts)
        {
            int success = 0, fail = 0;
            SlabShapeEditor slabShapeEditor;
            try
            {
                slabShapeEditor = roof.GetSlabShapeEditor();
                if (!slabShapeEditor.IsEnabled)
                    slabShapeEditor.Enable();
            }
            catch
            {
                return (0, pts.Count);
            }

            Face topFace = GetTopFace(roof);
            if (topFace == null) return (0, pts.Count);

            foreach (XYZ p in pts)
            {
                try
                {
                    IntersectionResult ir = topFace.Project(p);
                    XYZ proj = ir != null ? ir.XYZPoint : new XYZ(p.X, p.Y, p.Z);
                    slabShapeEditor.AddPoint(proj); // <-- FIX: Use AddPoint instead of DrawPoint
                    success++;
                }
                catch
                {
                    fail++;
                }
            }
            return (success, fail);
        }

        private Face GetTopFace(Element elem)
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
                        if (f is PlanarFace pf && pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                        {
                            if (pf.Origin.Z > maxZ)
                            {
                                maxZ = pf.Origin.Z;
                                top = pf;
                            }
                        }
                    }
                }
            }
            return top;
        }
    }
}
