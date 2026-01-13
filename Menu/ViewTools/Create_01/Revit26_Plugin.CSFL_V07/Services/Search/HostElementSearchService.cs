using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.CSFL_V07.Enums;
using Revit26_Plugin.CSFL_V07.Helpers;
using Revit26_Plugin.CSFL_V07.Models;

namespace Revit26_Plugin.CSFL_V07.Services.Search
{
    /// <summary>
    /// Finds nearby Floors / Roofs in host and/or linked models,
    /// ranked by horizontal distance.
    /// </summary>
    public class HostElementSearchService
    {
        private readonly Document _doc;

        public HostElementSearchService(Document doc)
        {
            _doc = doc;
        }

        public List<CandidateHostElement> FindCandidates(
            XYZ midPoint,
            double thresholdMm,
            SnapSourceMode mode)
        {
            double thresholdFt = UnitConversionHelper.MmToFt(thresholdMm);
            var results = new List<CandidateHostElement>();

            if (mode == SnapSourceMode.HostOnly || mode == SnapSourceMode.HostAndLinked)
            {
                CollectFromDocument(_doc, Transform.Identity, midPoint, thresholdFt, results);
            }

            if (mode == SnapSourceMode.LinkedOnly || mode == SnapSourceMode.HostAndLinked)
            {
                CollectFromLinks(midPoint, thresholdFt, results);
            }

            return results
                .OrderBy(r => r.Distance)   // first found wins if same distance
                .ToList();
        }

        // ---------------- PRIVATE ----------------

        private void CollectFromDocument(
            Document doc,
            Transform transform,
            XYZ midPoint,
            double thresholdFt,
            List<CandidateHostElement> results)
        {
            var collector = new FilteredElementCollector(doc)
                .WherePasses(new LogicalOrFilter(
                    new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                    new ElementCategoryFilter(BuiltInCategory.OST_Roofs)))
                .WhereElementIsNotElementType();

            foreach (var el in collector)
            {
                BoundingBoxXYZ bb = el.get_BoundingBox(null);
                if (bb == null) continue;

                XYZ min = transform.OfPoint(bb.Min);
                XYZ max = transform.OfPoint(bb.Max);

                if (Math.Abs(min.Z - midPoint.Z) > thresholdFt &&
                    Math.Abs(max.Z - midPoint.Z) > thresholdFt)
                    continue;

                double dx = Math.Max(0, Math.Max(min.X - midPoint.X, midPoint.X - max.X));
                double dy = Math.Max(0, Math.Max(min.Y - midPoint.Y, midPoint.Y - max.Y));
                double dist = Math.Sqrt(dx * dx + dy * dy);

                results.Add(new CandidateHostElement(el, bb, dist));
            }
        }

        private void CollectFromLinks(
            XYZ midPoint,
            double thresholdFt,
            List<CandidateHostElement> results)
        {
            var linkInstances = new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>();

            foreach (var link in linkInstances)
            {
                Document linkDoc = link.GetLinkDocument();
                if (linkDoc == null) continue;

                Transform linkTransform = link.GetTotalTransform();

                CollectFromDocument(
                    linkDoc,
                    linkTransform,
                    midPoint,
                    thresholdFt,
                    results);
            }
        }
    }
}
