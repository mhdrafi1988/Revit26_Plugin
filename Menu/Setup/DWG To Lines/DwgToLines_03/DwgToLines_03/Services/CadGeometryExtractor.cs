using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Windows.Media;
using Revit26_Plugin.DwgSymbolicConverter_V03.Models;
using DBTransform = Autodesk.Revit.DB.Transform;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Services
{
    public static class CadGeometryExtractor
    {
        public record ExtractedCurve(Curve Curve, string Layer);

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

            foreach (GeometryObject g in import.get_Geometry(opt))
            {
                if (g is GeometryInstance gi)
                {
                    DBTransform t = t0.Multiply(gi.Transform);

                    foreach (GeometryObject o in gi.GetInstanceGeometry())
                    {
                        string layer = ResolveLayer(o, doc);

                        if (o is Curve c)
                            result.Add(new(c.CreateTransformed(t), layer));

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
            return result;
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
