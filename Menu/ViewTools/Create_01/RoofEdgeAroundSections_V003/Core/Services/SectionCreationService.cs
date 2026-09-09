using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofEdgeAroundSections.V003
{
    /// <summary>
    /// Creates ViewSection elements from the confirmed (checked) rows of the plan.
    /// One Transaction per roof (all directions for that roof committed together),
    /// so a partial failure on one roof does not leave orphan sections for that
    /// roof, while a failure on one roof does not abort other roofs' runs.
    /// </summary>
    public class SectionCreationService
    {
        public RunResult CreateSections(
            Document doc,
            IEnumerable<PlannedSection> rowsToProcess,
            RoofEdgeSectionsSettings settings,
            ViewFamilyType sectionViewFamilyType,
            ViewTemplateOption viewTemplate,
            ObservableCollection<LogEntry> log)
        {
            var result = new RunResult();

            double offsetFeet = UnitUtils.ConvertToInternalUnits(settings.OffsetMm, UnitTypeId.Millimeters);
            double edgeDepthFeet = UnitUtils.ConvertToInternalUnits(settings.EdgeDepthMm, UnitTypeId.Millimeters);
            double cropHeightFeet = UnitUtils.ConvertToInternalUnits(settings.CropHeightMm, UnitTypeId.Millimeters);
            double searchDistanceFeet = UnitUtils.ConvertToInternalUnits(settings.SearchDistanceMm, UnitTypeId.Millimeters);
            double marginOffsetFeet = UnitUtils.ConvertToInternalUnits(settings.MarginOffsetMm, UnitTypeId.Millimeters);

            // Resolved once per Run rather than once per row — the ReferenceIntersector's
            // View3D context doesn't change between rows, so re-collecting it per row was
            // redundant FilteredElementCollector work repeated for every planned section.
            View3D searchView3D = NearbyWallFinder.FindAny3DView(doc);
            if (searchView3D == null)
            {
                log.Add(new LogEntry(LogLevel.Warning,
                    "No 3D view available in document for wall search — all sections will use Search Distance as fallback crop width."));
            }

            var byRoof = rowsToProcess
                .Where(r => r.IsIncluded && r.Status == PlannedSectionStatus.Ready)
                .GroupBy(r => r.RoofId);

            foreach (var roofGroup in byRoof)
            {
                using (Transaction t = new Transaction(doc, $"Create Roof Edge Sections — Roof {roofGroup.Key.Value}"))
                {
                    t.Start();
                    int thisRoofCreated = 0; // per-roof, not the cumulative CreatedViewIds.Count —
                                              // fixes over-subtraction on rollback when an earlier
                                              // roof in the same Run already succeeded.
                    try
                    {
                        foreach (PlannedSection row in roofGroup)
                        {
                            log.Add(new LogEntry(LogLevel.Info,
                                $"Creating {row.SectionViewName} (edge length {row.EdgeLengthMm:F0} mm)..."));

                            try
                            {
                                BoundingBoxXYZ sectionBox = BuildSectionBoundingBox(
                                    doc, row, searchView3D, offsetFeet, edgeDepthFeet, cropHeightFeet, searchDistanceFeet, marginOffsetFeet, log);

                                ViewSection view = ViewSection.CreateSection(doc, sectionViewFamilyType.Id, sectionBox);
                                view.Name = row.SectionViewName;

                                if (viewTemplate != null && viewTemplate.TemplateId != ElementId.InvalidElementId)
                                {
                                    view.ViewTemplateId = viewTemplate.TemplateId;
                                }

                                result.CreatedViewIds.Add(view.Id);
                                result.CreatedCount++;
                                thisRoofCreated++;

                                log.Add(new LogEntry(LogLevel.Success, $"{row.SectionViewName} created (View Id {view.Id.Value})."));
                            }
                            catch (Exception exRow)
                            {
                                result.FailedCount++;
                                log.Add(new LogEntry(LogLevel.Warning, $"{row.SectionViewName}: creation failed — {exRow.Message}. Skipped."));
                            }
                        }

                        TransactionStatus status = t.Commit();
                        if (status != TransactionStatus.Committed)
                        {
                            log.Add(new LogEntry(LogLevel.Error,
                                $"Roof {roofGroup.Key.Value}: transaction did not commit (status: {status}). All sections for this roof rolled back."));

                            // Reconcile counters using ONLY this roof's created count — subtracting
                            // the cumulative CreatedViewIds.Count here would incorrectly wipe out
                            // successes from earlier roofs already committed in this same Run.
                            result.CreatedCount -= thisRoofCreated;
                            result.CreatedViewIds.RemoveRange(
                                result.CreatedViewIds.Count - thisRoofCreated, thisRoofCreated);
                            result.FailedCount += roofGroup.Count();
                        }
                    }
                    catch (Exception exRoof)
                    {
                        if (t.GetStatus() == TransactionStatus.Started)
                            t.RollBack();

                        result.CreatedCount -= thisRoofCreated;
                        if (thisRoofCreated > 0)
                        {
                            result.CreatedViewIds.RemoveRange(
                                result.CreatedViewIds.Count - thisRoofCreated, thisRoofCreated);
                        }

                        log.Add(new LogEntry(LogLevel.Error,
                            $"Roof {roofGroup.Key.Value}: unexpected error, transaction rolled back — {exRoof.Message}"));
                        result.FailedCount += roofGroup.Count();
                    }
                }
            }

            // Rows skipped before Run (NoEdgeFound / MergedOut / unchecked) count toward SkippedCount.
            result.SkippedCount = rowsToProcess.Count(r => !r.IsIncluded || r.Status != PlannedSectionStatus.Ready);

            log.Add(new LogEntry(LogLevel.Success, $"Run complete — {result.SummaryLine}"));

            return result;
        }

        /// <summary>
        /// Builds the section's bounding box.
        ///
        /// GEOMETRY FIX (confirmed with Rafi): the view/cut-line assignment is swapped
        /// from V001. Previously viewDir = InwardNormal, which made the cut line run
        /// PARALLEL to the roof edge (looking across the roof). Rafi's actual need is to
        /// see how the roof meets the wall/object below it — i.e. look ALONG the edge,
        /// with the cut plane slicing THROUGH the edge perpendicular to it. So now:
        ///   viewDir (BasisZ, look direction)      = edge tangent
        ///   rightDir (BasisX, cut/search axis)     = InwardNormal (perpendicular to edge, into roof)
        /// Origin remains the edge midpoint, pulled back along InwardNormal by the offset.
        ///
        /// Crop width (perpendicular axis, into roof/wall) is now dynamic: search for a
        /// nearby wall from the offset origin along InwardNormal, out to SearchDistanceMm.
        /// If found, width = wall thickness + MarginOffsetMm past its far face. If not
        /// found (or wall has no valid Width, e.g. curtain wall), width falls back to
        /// SearchDistanceMm and a warning is logged.
        ///
        /// EdgeDepthMm serves both the crop width along the edge tangent AND the camera's
        /// far-clip depth (single value, per Rafi confirmation — not split into two fields).
        /// </summary>
        private static BoundingBoxXYZ BuildSectionBoundingBox(
            Document doc,
            PlannedSection row,
            View3D searchView3D,
            double offsetFeet,
            double edgeDepthFeet,
            double cropHeightFeet,
            double searchDistanceFeet,
            double marginOffsetFeet,
            ObservableCollection<LogEntry> log)
        {
            XYZ upDir = XYZ.BasisZ;

            // Along-edge direction, derived from InwardNormal (perpendicular to edge, in-plane)
            // crossed with up. This is now the VIEW direction (was previously rightDir).
            XYZ tangent = row.InwardNormal.CrossProduct(upDir).Normalize();
            XYZ viewDir = tangent;

            // Perpendicular to edge, into the roof — now the cut/search axis (was previously viewDir).
            XYZ rightDir = row.InwardNormal;

            // Origin: edge midpoint, pulled back along rightDir (InwardNormal) by the offset,
            // so the section origin sits clear of the edge. Wall search also originates here
            // (per Rafi confirmation — search from the offset point, not the raw edge midpoint,
            // so the found wall is consistent with what the final crop actually contains).
            XYZ origin = row.EdgeMidpoint - rightDir * offsetFeet;

            Transform t = Transform.Identity;
            t.Origin = origin;
            t.BasisX = rightDir;
            t.BasisY = upDir;
            t.BasisZ = viewDir;

            // Dynamic crop width via nearby wall search. searchView3D is resolved once per
            // Run (see CreateSections) rather than re-collected here on every row.
            NearbyWallFinder.Result wallResult = NearbyWallFinder.FindNearbyWall(doc, searchView3D, origin, rightDir, searchDistanceFeet);

            double widthFeet;
            if (wallResult.SearchUnavailable)
            {
                widthFeet = searchDistanceFeet;
                log.Add(new LogEntry(LogLevel.Warning,
                    $"{row.SectionViewName}: no 3D view available in document for wall search — using Search Distance as fallback crop width."));
            }
            else if (wallResult.WallFound)
            {
                widthFeet = wallResult.WallWidthFeet + marginOffsetFeet;
                log.Add(new LogEntry(LogLevel.Info,
                    $"{row.SectionViewName}: wall found (Id {wallResult.FoundWall.Id.Value}), width {UnitUtils.ConvertFromInternalUnits(wallResult.WallWidthFeet, UnitTypeId.Millimeters):F0}mm + margin."));
            }
            else
            {
                widthFeet = searchDistanceFeet;
                log.Add(new LogEntry(LogLevel.Warning,
                    $"{row.SectionViewName}: no wall found within Search Distance — using Search Distance as fallback crop width."));
            }

            double halfEdgeDepth = edgeDepthFeet / 2.0;

            var box = new BoundingBoxXYZ
            {
                Transform = t,
                Min = new XYZ(-halfEdgeDepth, 0, 0),
                Max = new XYZ(halfEdgeDepth, cropHeightFeet, widthFeet)
            };

            return box;
        }
    }

    /// <summary>Thin wrapper describing the chosen View Template (or none).</summary>
    public class ViewTemplateOption
    {
        public string Name { get; set; }
        public ElementId TemplateId { get; set; } = ElementId.InvalidElementId;
    }
}
