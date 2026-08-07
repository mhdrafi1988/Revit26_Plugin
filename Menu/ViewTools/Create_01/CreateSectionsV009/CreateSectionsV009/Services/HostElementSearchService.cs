using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.CreateSectionsFromDetailLines.V009.Helpers;
using Revit26_Plugin.CreateSectionsFromDetailLines.V009.Models;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V009.Services
{
    /// <summary>
    /// Finds nearby Floors / Roofs in host and/or linked models,
    /// ranked by horizontal distance.
    ///
    /// V008 changes from V07:
    /// - SnapSourceMode enum replaced by HostSourceSelection (4 independent
    ///   category checkboxes: Floor/Roof x Host/Linked). Only checked
    ///   categories are collected/searched.
    /// - Z-acceptance rule changed: a candidate now qualifies only if the
    ///   line's Z is BELOW the candidate's bounding box, OR the line's Z
    ///   falls WITHIN the candidate's bounding box Z-range ("passing
    ///   through"). Candidates entirely ABOVE the line's Z are excluded
    ///   (this used to be a coarse threshold gate only; it is now the
    ///   actual qualification rule, folded in before ranking).
    /// - Once any qualifying candidate is found across the checked
    ///   categories, ranking proceeds by nearest horizontal distance as
    ///   before (2.b: still "nearest wins", the Z-rule is a filter, not
    ///   an override of the ranking).
    ///
    /// V009 changes from V008:
    /// - CandidateHostElement now carries HostSourceCategory, set here at
    ///   collection time (the only place that actually knows which of the
    ///   4 checked categories/passes produced it). Used by the new
    ///   "Sections To Create" preview grid's Host Type column.
    /// </summary>
    public class HostElementSearchService
    {
        private readonly Document _doc;

        public HostElementSearchService(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Returns qualifying candidates (below the line's Z or straddling it),
        /// ordered nearest-first. Empty list means no qualifying candidate was
        /// found in any checked category — caller decides whether to fall back
        /// to the threshold-based height or skip the line.
        /// </summary>
        /// <param name="midPoint">Line midpoint — used for horizontal (X/Y) distance ranking.</param>
        /// <param name="lineZ">Line's own elevation — used for the below/passing-through Z rule.</param>
        /// <param name="thresholdMm">Horizontal search radius in mm.</param>
        /// <param name="source">Which categories to search.</param>
        public List<CandidateHostElement> FindCandidates(
            XYZ midPoint,
            double lineZ,
            double thresholdMm,
            HostSourceSelection source)
        {
            double thresholdFt = UnitConversionHelper.MmToFt(thresholdMm);
            var results = new List<CandidateHostElement>();

            if (!source.AnySelected)
                return results;

            if (source.FloorHost)
                CollectFromDocument(_doc, Transform.Identity, midPoint, lineZ, thresholdFt,
                    BuiltInCategory.OST_Floors, HostSourceCategory.FloorHost, results);

            if (source.RoofHost)
                CollectFromDocument(_doc, Transform.Identity, midPoint, lineZ, thresholdFt,
                    BuiltInCategory.OST_Roofs, HostSourceCategory.RoofHost, results);

            if (source.FloorLinked)
                CollectFromLinks(midPoint, lineZ, thresholdFt, BuiltInCategory.OST_Floors, HostSourceCategory.FloorLinked, results);

            if (source.RoofLinked)
                CollectFromLinks(midPoint, lineZ, thresholdFt, BuiltInCategory.OST_Roofs, HostSourceCategory.RoofLinked, results);

            return results
                .OrderBy(r => r.Distance)   // nearest wins; first found wins if same distance
                .ToList();
        }

        // ---------------- PRIVATE ----------------

        private void CollectFromDocument(
            Document doc,
            Transform transform,
            XYZ midPoint,
            double lineZ,
            double thresholdFt,
            BuiltInCategory category,
            HostSourceCategory sourceCategory,
            List<CandidateHostElement> results)
        {
            var collector = new FilteredElementCollector(doc)
                .WherePasses(new ElementCategoryFilter(category))
                .WhereElementIsNotElementType();

            foreach (var el in collector)
            {
                BoundingBoxXYZ bb = el.get_BoundingBox(null);
                if (bb == null) continue;

                XYZ min = transform.OfPoint(bb.Min);
                XYZ max = transform.OfPoint(bb.Max);

                // Horizontal search radius gate (unchanged concept from V07,
                // now purely a distance cutoff — not used for Z qualification).
                double dx = Math.Max(0, Math.Max(min.X - midPoint.X, midPoint.X - max.X));
                double dy = Math.Max(0, Math.Max(min.Y - midPoint.Y, midPoint.Y - max.Y));
                double horizontalDist = Math.Sqrt(dx * dx + dy * dy);

                if (horizontalDist > thresholdFt)
                    continue;

                // Z-qualification rule (V008): line must be BELOW the element's
                // bounding box, or PASSING THROUGH it. Elements entirely above
                // the line's Z are excluded.
                bool isBelowLine = max.Z <= lineZ;         // element top at/below the line
                bool isPassingThrough = lineZ >= min.Z && lineZ <= max.Z;

                if (!isBelowLine && !isPassingThrough)
                    continue;

                results.Add(new CandidateHostElement(el, bb, horizontalDist, sourceCategory));
            }
        }

        private void CollectFromLinks(
            XYZ midPoint,
            double lineZ,
            double thresholdFt,
            BuiltInCategory category,
            HostSourceCategory sourceCategory,
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
                    lineZ,
                    thresholdFt,
                    category,
                    sourceCategory,
                    results);
            }
        }
    }
}
