using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using Revit26_Plugin.DwgSymbolicConverter_V01.Models;

namespace Revit26_Plugin.DwgSymbolicConverter_V01.Services
{
    /// <summary>
    /// Extracts real curve geometry from a DWG ImportInstance.
    /// Read-only. No transactions.
    /// </summary>
    public static class CadGeometryExtractor
    {
        public static List<(Curve curve, string layer)> Extract(
            ImportInstance cadInstance,
            Document doc,
            SplineHandlingMode splineMode,
            Action<string, Brush> log)
        {
            var results = new List<(Curve curve, string layer)>();

            Options options = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geometry = cadInstance.get_Geometry(options);

            log("[INFO] Starting DWG geometry extraction", Brushes.White);

            foreach (GeometryObject geoObj in geometry)
            {
                if (geoObj is GeometryInstance geomInstance)
                {
                    ExtractFromInstance(
                        geomInstance,
                        doc,
                        splineMode,
                        results);
                }
            }

            log($"[INFO] Curves extracted: {results.Count}", Brushes.White);
            return results;
        }

        private static void ExtractFromInstance(
            GeometryInstance geomInstance,
            Document doc,
            SplineHandlingMode splineMode,
            List<(Curve curve, string layer)> output)
        {
            Autodesk.Revit.DB.Transform transform = geomInstance.Transform;
            GeometryElement instanceGeometry = geomInstance.GetInstanceGeometry();

            foreach (GeometryObject obj in instanceGeometry)
            {
                string layer = ResolveLayer(obj, doc);

                if (obj is Curve curve)
                {
                    Curve transformed = curve.CreateTransformed(transform);
                    HandleCurve(transformed, layer, splineMode, output);
                }
                else if (obj is PolyLine polyLine)
                {
                    IList<XYZ> pts = polyLine.GetCoordinates();
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        Line seg = Line.CreateBound(
                            transform.OfPoint(pts[i]),
                            transform.OfPoint(pts[i + 1]));

                        output.Add((seg, layer));
                    }
                }
            }
        }

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
                {
                    output.Add((spline, layer));
                }
                else
                {
                    Tessellate(curve, layer, output);
                }

                return;
            }

            // Fallback for Bezier / unsupported curves
            Tessellate(curve, layer, output);
        }

        private static void Tessellate(
            Curve curve,
            string layer,
            List<(Curve curve, string layer)> output)
        {
            IList<XYZ> pts = curve.Tessellate();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                output.Add((
                    Line.CreateBound(pts[i], pts[i + 1]),
                    layer));
            }
        }

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
