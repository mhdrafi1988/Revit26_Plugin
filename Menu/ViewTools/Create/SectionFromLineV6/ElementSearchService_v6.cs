using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.PlanSections.Services
{
    /// <summary>
    /// v6.2 — Collect full XYZ bounding extents from host + linked Floor/Roof elements.
    /// </summary>
    public class ElementSearchService_v6
    {
        private readonly Document _doc;

        public ElementSearchService_v6(Document doc)
        {
            _doc = doc;
        }

        public class MultiBBoxResult
        {
            public bool FoundAny { get; set; }

            public List<double> XValues { get; set; } = new List<double>();
            public List<double> YValues { get; set; } = new List<double>();
            public List<double> ZValues { get; set; } = new List<double>();

            public int HostHits { get; set; }
            public int LinkHits { get; set; }
        }

        public MultiBBoxResult CollectBounds(XYZ refMid, double levelZ, double thresholdFt, SnapSourceMode mode)
        {
            var result = new MultiBBoxResult();

            if (mode == SnapSourceMode.HostOnly || mode == SnapSourceMode.HostAndLinked)
                SearchHost(result, levelZ, thresholdFt);

            if (mode == SnapSourceMode.LinkedOnly || mode == SnapSourceMode.HostAndLinked)
                SearchLinked(result, levelZ, thresholdFt);

            result.FoundAny = result.ZValues.Any();
            return result;
        }

        private void SearchHost(MultiBBoxResult result, double levelZ, double thresholdFt)
        {
            var elems = new FilteredElementCollector(_doc)
                .WherePasses(new LogicalOrFilter(
                    new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                    new ElementCategoryFilter(BuiltInCategory.OST_Roofs)))
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (var el in elems)
            {
                var bb = el.get_BoundingBox(null);
                if (bb == null) continue;

                if (!WithinZ(bb, levelZ, thresholdFt)) continue;

                AddBBox(bb, result);
                result.HostHits++;
            }
        }

        private void SearchLinked(MultiBBoxResult result, double levelZ, double thresholdFt)
        {
            var links = new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>();

            foreach (var link in links)
            {
                Document linkDoc = link.GetLinkDocument();
                if (linkDoc == null) continue;

                Transform trf = link.GetTotalTransform();

                var elems = new FilteredElementCollector(linkDoc)
                    .WherePasses(new LogicalOrFilter(
                        new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                        new ElementCategoryFilter(BuiltInCategory.OST_Roofs)))
                    .WhereElementIsNotElementType()
                    .ToElements();

                foreach (var el in elems)
                {
                    var bb = el.get_BoundingBox(null);
                    if (bb == null) continue;

                    var hostBB = TransformBBox(bb, trf);

                    if (!WithinZ(hostBB, levelZ, thresholdFt)) continue;

                    AddBBox(hostBB, result);
                    result.LinkHits++;
                }
            }
        }

        private BoundingBoxXYZ TransformBBox(BoundingBoxXYZ bb, Transform trf)
        {
            var newBB = new BoundingBoxXYZ();

            var pts = new List<XYZ>
            {
                new XYZ(bb.Min.X, bb.Min.Y, bb.Min.Z),
                new XYZ(bb.Max.X, bb.Min.Y, bb.Min.Z),
                new XYZ(bb.Min.X, bb.Max.Y, bb.Min.Z),
                new XYZ(bb.Max.X, bb.Max.Y, bb.Min.Z),

                new XYZ(bb.Min.X, bb.Min.Y, bb.Max.Z),
                new XYZ(bb.Max.X, bb.Min.Y, bb.Max.Z),
                new XYZ(bb.Min.X, bb.Max.Y, bb.Max.Z),
                new XYZ(bb.Max.X, bb.Max.Y, bb.Max.Z)
            };

            var hostPts = pts.Select(p => trf.OfPoint(p)).ToList();

            newBB.Min = new XYZ(
                hostPts.Min(p => p.X),
                hostPts.Min(p => p.Y),
                hostPts.Min(p => p.Z));

            newBB.Max = new XYZ(
                hostPts.Max(p => p.X),
                hostPts.Max(p => p.Y),
                hostPts.Max(p => p.Z));

            return newBB;
        }

        private void AddBBox(BoundingBoxXYZ bb, MultiBBoxResult result)
        {
            result.XValues.Add(bb.Min.X);
            result.XValues.Add(bb.Max.X);

            result.YValues.Add(bb.Min.Y);
            result.YValues.Add(bb.Max.Y);

            result.ZValues.Add(bb.Min.Z);
            result.ZValues.Add(bb.Max.Z);
        }

        private bool WithinZ(BoundingBoxXYZ bb, double levelZ, double thresholdFt)
        {
            return Math.Abs(bb.Min.Z - levelZ) <= thresholdFt ||
                   Math.Abs(bb.Max.Z - levelZ) <= thresholdFt;
        }
    }
}
