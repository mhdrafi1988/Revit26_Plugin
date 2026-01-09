using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit22_Plugin.AutoRoofSections.Services
{
    public class RoofEdgeExtractor
    {
        private readonly Document _doc;
        private readonly Action<string> _log;

        public RoofEdgeExtractor(Document doc, Action<string> log)
        {
            _doc = doc;
            _log = log;
        }

        public List<RoofEdgeInfo> GetValidEdges(RoofBase roof, double minLengthMm)
        {
            var edges = new List<RoofEdgeInfo>();
            double minFt = UnitUtils.ConvertToInternalUnits(minLengthMm, UnitTypeId.Millimeters);

            Options opt = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geo = roof.get_Geometry(opt);
            if (geo == null)
            {
                _log("No geometry found for roof.");
                return edges;
            }

            foreach (var obj in geo)
            {
                if (!(obj is Solid solid)) continue;
                if (solid.Faces.IsEmpty || solid.Edges.IsEmpty) continue;

                foreach (Edge e in solid.Edges)
                {
                    Curve c = e.AsCurve();
                    if (!(c is Line line)) continue; // skip arcs

                    double len = line.Length;
                    if (len < minFt)
                    {
                        _log($"Skipping short edge (len = {len} ft)");
                        continue;
                    }

                    XYZ p1 = line.GetEndPoint(0);
                    XYZ p2 = line.GetEndPoint(1);
                    XYZ mid = (p1 + p2) / 2.0;

                    XYZ dir = (p2 - p1).Normalize();

                    edges.Add(new RoofEdgeInfo
                    {
                        Start = p1,
                        End = p2,
                        Midpoint = mid,
                        Direction = dir
                    });
                }
            }

            return edges;
        }
    }

    public class RoofEdgeInfo
    {
        public XYZ Start { get; set; }
        public XYZ End { get; set; }
        public XYZ Midpoint { get; set; }
        public XYZ Direction { get; set; }
    }
}
