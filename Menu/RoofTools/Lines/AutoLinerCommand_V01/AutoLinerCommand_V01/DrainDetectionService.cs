using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.AutoLiner_V01.Helpers;

namespace Revit26_Plugin.AutoLiner_V01.Services
{
    public class CornerDetectionService
    {
        public List<XYZ> DetectFootprintCorners(RoofBase roof)
        {
            var corners = new List<XYZ>();

            if (roof == null)
                return corners;

            var footprintCurves = GetFootprintCurves(roof);
            if (footprintCurves.Count == 0)
                return corners;

            double minEdge = GeometryTolerance.MmToFt(100); // 100 mm

            foreach (var curve in footprintCurves)
            {
                if (curve.Length < minEdge)
                    continue;

                corners.Add(curve.GetEndPoint(0));
                corners.Add(curve.GetEndPoint(1));
            }

            return MergeCornerPoints(corners);
        }

        // =====================================================
        // GET FOOTPRINT CURVES
        // =====================================================
        private List<Curve> GetFootprintCurves(RoofBase roof)
        {
            var curves = new List<Curve>();

            try
            {
                // Use the FootprintRoof type to get footprint curves if possible
                if (roof is Autodesk.Revit.DB.FootPrintRoof footprintRoof)
                {
                    ModelCurveArrArray profiles = footprintRoof.GetProfiles();
                    for (int arrIdx = 0; arrIdx < profiles.Size; arrIdx++)
                    {
                        ModelCurveArray footprint = profiles.get_Item(arrIdx);
                        foreach (ModelCurve modelCurve in footprint)
                        {
                            Curve c = modelCurve.GeometryCurve;
                            if (c is Line)
                                curves.Add(c);
                        }
                    }
                }
                // For other roof types, try to get boundary from geometry
                else
                {
                    Options options = new Options();
                    GeometryElement geomElem = roof.get_Geometry(options);
                    foreach (GeometryObject geomObj in geomElem)
                    {
                        if (geomObj is GeometryInstance geomInst)
                        {
                            GeometryElement instElem = geomInst.GetInstanceGeometry();
                            foreach (GeometryObject instObj in instElem)
                            {
                                if (instObj is Curve c && c is Line)
                                    curves.Add(c);
                            }
                        }
                        else if (geomObj is Curve c && c is Line)
                        {
                            curves.Add(c);
                        }
                    }
                }
            }
            catch
            {
                // some roof types won't expose profiles
            }

            return curves;
        }

        // =====================================================
        // MERGE CORNER CLUSTERS
        // =====================================================
        private List<XYZ> MergeCornerPoints(List<XYZ> points)
        {
            var result = new List<XYZ>();
            var used = new HashSet<int>();

            double mergeTol = GeometryTolerance.MmToFt(50); // 50 mm

            for (int i = 0; i < points.Count; i++)
            {
                if (used.Contains(i))
                    continue;

                var cluster = new List<XYZ> { points[i] };
                used.Add(i);

                for (int j = i + 1; j < points.Count; j++)
                {
                    if (used.Contains(j))
                        continue;

                    if (points[i].DistanceTo(points[j]) < mergeTol)
                    {
                        cluster.Add(points[j]);
                        used.Add(j);
                    }
                }

                result.Add(Centroid(cluster));
            }

            return result;
        }

        private XYZ Centroid(List<XYZ> pts)
        {
            return new XYZ(
                pts.Average(p => p.X),
                pts.Average(p => p.Y),
                pts.Average(p => p.Z));
        }
    }
}
