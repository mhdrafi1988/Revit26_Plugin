using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Windows.Media;
using Revit26_Plugin.DwgToDetailLines.V002.Models;
using DBTransform = Autodesk.Revit.DB.Transform;

namespace Revit26_Plugin.DwgToDetailLines.V002.Services
{
    public static class CadGeometryExtractor
    {
        public record ExtractedCurve(Curve Curve, string Layer);

        /// <summary>
        /// Extracts curves from a linked/imported CAD instance.
        /// SplineHandlingMode.Preserve keeps HermiteSpline/NurbSpline geometry intact
        /// (as a NurbSpline, per Revit's native curve representation).
        /// SplineHandlingMode.Tessellate breaks any spline into straight Line segments
        /// using the curve's tessellated point list.
        /// </summary>
        public static List<ExtractedCurve> Extract(
            ImportInstance import,
            Document doc,
            View view,
            SplineHandlingMode splineMode,
            System.Action<string, Brush> log)
        {
            var result = new List<ExtractedCurve>();

            Options opt = new Options { View = view };
            DBTransform t0 = import.GetTransform();

            int splineCount = 0;

            foreach (GeometryObject g in import.get_Geometry(opt))
            {
                if (g is GeometryInstance gi)
                {
                    DBTransform t = t0.Multiply(gi.Transform);

                    foreach (GeometryObject o in gi.GetInstanceGeometry())
                    {
                        string layer = ResolveLayer(o, doc);

                        if (o is Curve c)
                        {
                            AddCurve(result, c, t, layer, splineMode, ref splineCount);
                        }
                        else if (o is PolyLine pl)
                        {
                            var pts = pl.GetCoordinates();
                            for (int i = 0; i < pts.Count - 1; i++)
                                result.Add(new(
                                    Line.CreateBound(
                                        t.OfPoint(pts[i]),
                                        t.OfPoint(pts[i + 1])),
                                    layer));
                        }
                    }
                }
            }

            log?.Invoke($"[INFO] Extracted {result.Count} curves", Brushes.White);

            if (splineCount > 0)
            {
                string modeText = splineMode == SplineHandlingMode.Preserve
                    ? "preserved as NurbSpline"
                    : "tessellated to line segments";
                log?.Invoke($"[INFO] {splineCount} spline curve(s) {modeText}", Brushes.White);
            }

            return result;
        }

        private static void AddCurve(
            List<ExtractedCurve> result,
            Curve c,
            DBTransform t,
            string layer,
            SplineHandlingMode splineMode,
            ref int splineCount)
        {
            bool isSpline = c is HermiteSpline || c is NurbSpline;

            if (!isSpline)
            {
                result.Add(new(c.CreateTransformed(t), layer));
                return;
            }

            splineCount++;

            if (splineMode == SplineHandlingMode.Preserve)
            {
                // Ensure we hand back a NurbSpline (Revit's native curve for
                // detail-curve creation); HermiteSpline is converted via its
                // control/tangent data through CreateTransformed, which Revit
                // resolves internally to an equivalent bound curve.
                result.Add(new(c.CreateTransformed(t), layer));
            }
            else
            {
                // Tessellate: break into straight segments using Revit's
                // internal tessellation (respects curve tolerance).
                IList<XYZ> pts = c.Tessellate();

                for (int i = 0; i < pts.Count - 1; i++)
                {
                    XYZ p0 = t.OfPoint(pts[i]);
                    XYZ p1 = t.OfPoint(pts[i + 1]);

                    if (p0.IsAlmostEqualTo(p1))
                        continue;

                    result.Add(new(Line.CreateBound(p0, p1), layer));
                }
            }
        }

        /// <summary>
        /// Lightweight pre-scan for the Metrics Card: counts raw geometry entities
        /// and distinct layers in a CAD import, without building full curve output.
        /// Used to populate "CAD Import Info" immediately on selection.
        /// </summary>
        public static (int entityCount, int layerCount) PreScan(
            ImportInstance import,
            Document doc,
            View view)
        {
            Options opt = new Options { View = view };
            var layers = new HashSet<string>();
            int entityCount = 0;

            foreach (GeometryObject g in import.get_Geometry(opt))
            {
                if (g is GeometryInstance gi)
                {
                    foreach (GeometryObject o in gi.GetInstanceGeometry())
                    {
                        if (o is Curve || o is PolyLine)
                        {
                            entityCount++;
                            layers.Add(ResolveLayer(o, doc));
                        }
                    }
                }
            }

            return (entityCount, layers.Count);
        }

        private static string ResolveLayer(GeometryObject o, Document d)
        {
            if (o.GraphicsStyleId == ElementId.InvalidElementId)
                return "DWG-Default";

            return (d.GetElement(o.GraphicsStyleId) as GraphicsStyle)?
                .GraphicsStyleCategory?.Name ?? "DWG-Default";
        }
    }
}
