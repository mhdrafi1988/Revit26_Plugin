using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofEdgeSections.V001
{
    /// <summary>
    /// Builds the Planned Sections preview list from a set of pre-selected roofs:
    /// buckets edges (RoofEdgeBucketingEngine), builds names + dedup checks
    /// (SectionNamingService), and produces one PlannedSection row per direction
    /// per roof (Ready / AlreadyExists / NoEdgeFound).
    /// </summary>
    public class SectionPlanBuilder
    {
        public ObservableCollection<PlannedSection> BuildPlan(
            Document doc,
            IList<Element> selectedRoofs,
            IList<Element> skippedNonRoofElements,
            double viewRotationRadians,
            ObservableCollection<LogEntry> log)
        {
            var plan = new ObservableCollection<PlannedSection>();

            log.Add(new LogEntry(LogLevel.Info, $"Selection captured: {selectedRoofs.Count + skippedNonRoofElements.Count} elements."));
            if (skippedNonRoofElements.Count > 0)
            {
                log.Add(new LogEntry(LogLevel.Warning,
                    $"Skipped {skippedNonRoofElements.Count} non-roof elements ({string.Join(", ", skippedNonRoofElements.Select(e => e.Category?.Name ?? "Unknown").Distinct())})."));
            }

            if (selectedRoofs.Count == 0)
            {
                log.Add(new LogEntry(LogLevel.Warning, "No roof elements in selection — nothing to plan."));
                return plan;
            }

            HashSet<string> existingViewNames = SectionNamingService.GetExistingViewNames(doc);

            log.Add(new LogEntry(LogLevel.Info, $"Bucketing edges by view-aligned bounding box for {selectedRoofs.Count} roofs."));

            foreach (Element el in selectedRoofs)
            {
                if (el is not RoofBase roof)
                {
                    // Defensive — should already be filtered before this point.
                    log.Add(new LogEntry(LogLevel.Warning, $"Element {el.Id.Value} is not a RoofBase — skipped."));
                    continue;
                }

                BoundingBoxXYZ bbox = roof.get_BoundingBox(null);
                if (bbox == null)
                {
                    log.Add(new LogEntry(LogLevel.Warning, $"Roof {roof.Id.Value}: no bounding box available — skipped entirely."));
                    continue;
                }

                string roofDisplayName = SectionNamingService.GetRoofDisplayName(roof);

                var bucketed = RoofEdgeBucketingEngine.BucketEdges(roof, bbox, viewRotationRadians, log);

                foreach (EdgeDirection dir in Enum.GetValues(typeof(EdgeDirection)).Cast<EdgeDirection>())
                {
                    if (!bucketed.TryGetValue(dir, out var edge))
                    {
                        plan.Add(new PlannedSection
                        {
                            RoofId = roof.Id,
                            RoofDisplayName = roofDisplayName,
                            Direction = dir,
                            SectionViewName = "—",
                            EdgeLengthMm = 0,
                            Status = PlannedSectionStatus.NoEdgeFound,
                            IsIncluded = false,
                            RoofBoundingBox = bbox
                        });
                        continue;
                    }

                    string viewName = SectionNamingService.BuildSectionViewName(roofDisplayName, dir);
                    bool alreadyExists = existingViewNames.Contains(viewName);

                    if (alreadyExists)
                    {
                        log.Add(new LogEntry(LogLevel.Warning, $"{viewName} already exists — skipped."));
                    }

                    plan.Add(new PlannedSection
                    {
                        RoofId = roof.Id,
                        RoofDisplayName = roofDisplayName,
                        Direction = dir,
                        SectionViewName = viewName,
                        EdgeLengthMm = UnitUtils.ConvertFromInternalUnits(edge.LengthFeet, UnitTypeId.Millimeters),
                        EdgeCurve = edge.Curve,
                        EdgeMidpoint = edge.Midpoint,
                        InwardNormal = edge.InwardNormal,
                        RoofBoundingBox = bbox,
                        Status = alreadyExists ? PlannedSectionStatus.AlreadyExists : PlannedSectionStatus.Ready,
                        IsIncluded = !alreadyExists
                    });
                }
            }

            int ready = plan.Count(p => p.Status == PlannedSectionStatus.Ready);
            int exists = plan.Count(p => p.Status == PlannedSectionStatus.AlreadyExists);
            int noEdge = plan.Count(p => p.Status == PlannedSectionStatus.NoEdgeFound);

            log.Add(new LogEntry(LogLevel.Success,
                $"Plan built: {ready} sections ready, {exists} skipped (existing), {noEdge} skipped (no edge)."));

            return plan;
        }
    }
}
