using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using Revit26_Plugin.DwgSymbolicConverter_V02.Models;

// ?? REQUIRED alias to avoid WPF conflict
using DBTransform = Autodesk.Revit.DB.Transform;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Services
{
    /// <summary>
    /// Extracts DWG geometry, applies full transform stack,
    /// and projects all curves onto the target sketch plane.
    /// </summary>
    public static class CadGeometryExtractor
    {
        public static List<(Curve curve, string layer)> Extract(
            ImportInstance cadInstance,
            Document doc,
            View targetView,
            SketchPlane targetSketchPlane,
            SplineHandlingMode splineMode,
            Action<string, Brush> log)
        {
            var results = new List<(Curve curve, string layer)>();

            if (targetSketchPlane == null)
                throw new InvalidOperationException("Target SketchPlane is null.");

            Plane targetPlane = targetSketchPlane.GetPlane();

            // ?? FULL import transform
            DBTransform importTransform = cadInstance.GetTransform();

            Options options = new Options
            {
                View = targetView,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine,
                ComputeReferences = false
            };

            GeometryElement geometry = cadInstance.get_Geometry(options);

            log("[INFO] Starting DWG geometry extraction", Brushes.White);
            log($"[INFO] Import transform origin: {importTransform.Origin}", Brushes.Cyan);

            foreach (GeometryObject geoObj in geometry)
            {
                if (geoObj is GeometryInstance geomInstance)
                {
                    ExtractFromInstance(
                        geomInstance,
                        importTransform,
                        targetPlane,
                        doc,
                        splineMode,
                        results);
                }
            }

            log($"[INFO] Curves extracted and projected: {results.Count}", Brushes.White);
            return results;
        }

        // ------------------------------------------------------------

        private static void ExtractFromInstance(
            GeometryInstance geomInstance,
            DBTransform importTransform,
            Plane targetPlane,
            Document doc,
            SplineHandlingMode splineMode,
            List<(Curve curve, string layer)> output)
        {
            DBTransform geomTransform = geomInstance.Transform;
            DBTransform totalTransform = importTransform.Multiply(geomTransform);

            GeometryElement instanceGeometry = geomInstance.GetInstanceGeometry();

            foreach (GeometryObject obj in instanceGeometry)
            {
                string layer = ResolveLayer(obj, doc);

                if (obj is Curve curve)
                {
                    Curve transformed = curve.CreateTransformed(totalTransform);
                    Curve projected = ProjectCurveToPlane(transformed, targetPlane);

                    HandleCurve(projected, layer, splineMode, output);
                }
                else if (obj is PolyLine polyLine)
                {
                    IList<XYZ> pts = polyLine.GetCoordinates();

                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        XYZ p0 = totalTransform.OfPoint(pts[i]);
                        XYZ p1 = totalTransform.OfPoint(pts[i + 1]);

                        XYZ p0p = ProjectPointToPlane(p0, targetPlane);
                        XYZ p1p = ProjectPointToPlane(p1, targetPlane);

                        if (!p0p.IsAlmostEqualTo(p1p))
                        {
                            output.Add((
                                Line.CreateBound(p0p, p1p),
                                layer));
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------

        private static void HandleCurve(
            Curve curve,
            string layer,
            SplineHandlingMode splineMode,
            List<(Curve curve, string layer)> output)
        {
            if (curve is Line || curve is Arc)
            {
                output.Add((curve, layer));
                return;
            }

            if (curve is NurbSpline spline)
            {
                if (splineMode == SplineHandlingMode.Preserve)
                    output.Add((spline, layer));
                else
                    Tessellate(curve, layer, output);

                return;
            }

            Tessellate(curve, layer, output);
        }

        // ------------------------------------------------------------

        private static void Tessellate(
            Curve curve,
            string layer,
            List<(Curve curve, string layer)> output)
        {
            IList<XYZ> pts = curve.Tessellate();

            for (int i = 0; i < pts.Count - 1; i++)
            {
                if (!pts[i].IsAlmostEqualTo(pts[i + 1]))
                {
                    output.Add((
                        Line.CreateBound(pts[i], pts[i + 1]),
                        layer));
                }
            }
        }

        // ------------------------------------------------------------

        private static Curve ProjectCurveToPlane(
            Curve curve,
            Plane plane)
        {
            XYZ p0 = ProjectPointToPlane(curve.GetEndPoint(0), plane);
            XYZ p1 = ProjectPointToPlane(curve.GetEndPoint(1), plane);

            return Line.CreateBound(p0, p1);
        }

        private static XYZ ProjectPointToPlane(
            XYZ point,
            Plane plane)
        {
            XYZ v = point - plane.Origin;
            double distance = v.DotProduct(plane.Normal);
            return point - distance * plane.Normal;
        }

        // ------------------------------------------------------------

        private static string ResolveLayer(
            GeometryObject obj,
            Document doc)
        {
            if (obj.GraphicsStyleId == ElementId.InvalidElementId)
                return "DWG-Default";

            GraphicsStyle gs =
                doc.GetElement(obj.GraphicsStyleId) as GraphicsStyle;

            return gs?.GraphicsStyleCategory?.Name ?? "DWG-Default";
        }
    }
}
