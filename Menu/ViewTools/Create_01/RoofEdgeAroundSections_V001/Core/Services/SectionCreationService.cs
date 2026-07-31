using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofEdgeSections.V001
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
            double depthFeet = UnitUtils.ConvertToInternalUnits(settings.SectionDepthMm, UnitTypeId.Millimeters);
            double cropHeightFeet = UnitUtils.ConvertToInternalUnits(settings.CropHeightMm, UnitTypeId.Millimeters);
            double fixedCropWidthFeet = UnitUtils.ConvertToInternalUnits(settings.FixedCropWidthMm, UnitTypeId.Millimeters);

            var byRoof = rowsToProcess
                .Where(r => r.IsIncluded && r.Status == PlannedSectionStatus.Ready)
                .GroupBy(r => r.RoofId);

            foreach (var roofGroup in byRoof)
            {
                using (Transaction t = new Transaction(doc, $"Create Roof Edge Sections — Roof {roofGroup.Key.Value}"))
                {
                    t.Start();
                    try
                    {
                        foreach (PlannedSection row in roofGroup)
                        {
                            log.Add(new LogEntry(LogLevel.Info,
                                $"Creating {row.SectionViewName} (edge length {row.EdgeLengthMm:F0} mm)..."));

                            try
                            {
                                BoundingBoxXYZ sectionBox = BuildSectionBoundingBox(
                                    row, offsetFeet, depthFeet, cropHeightFeet, fixedCropWidthFeet, settings.CropWidthMode);

                                ViewSection view = ViewSection.CreateSection(doc, sectionViewFamilyType.Id, sectionBox);
                                view.Name = row.SectionViewName;

                                if (viewTemplate != null && viewTemplate.TemplateId != ElementId.InvalidElementId)
                                {
                                    view.ViewTemplateId = viewTemplate.TemplateId;
                                }

                                result.CreatedViewIds.Add(view.Id);
                                result.CreatedCount++;

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
                            // Reconcile counters — none of this roof's rows actually persisted.
                            int thisRoofAttempted = roofGroup.Count();
                            result.CreatedCount -= result.CreatedViewIds.Count; // safety no-op if already 0
                            result.FailedCount += thisRoofAttempted;
                        }
                    }
                    catch (Exception exRoof)
                    {
                        if (t.GetStatus() == TransactionStatus.Started)
                            t.RollBack();

                        log.Add(new LogEntry(LogLevel.Error,
                            $"Roof {roofGroup.Key.Value}: unexpected error, transaction rolled back — {exRoof.Message}"));
                        result.FailedCount += roofGroup.Count();
                    }
                }
            }

            // Rows skipped before Run (AlreadyExists / NoEdgeFound / unchecked) count toward SkippedCount.
            result.SkippedCount = rowsToProcess.Count(r => !r.IsIncluded || r.Status != PlannedSectionStatus.Ready);

            log.Add(new LogEntry(LogLevel.Success, $"Run complete — {result.SummaryLine}"));

            return result;
        }

        /// <summary>
        /// Builds the section's bounding box: origin at the edge midpoint offset
        /// outward by settings.OffsetMm, view direction = inward normal, depth =
        /// far clip, height = crop height.
        ///
        /// Width (along-edge dimension, "halfWidth" below) is ALWAYS FixedCropWidthMm,
        /// producing a short perpendicular stub centered on the edge midpoint — per
        /// Rafi confirmation. "TightToEdgeSpan" previously stretched this dimension to
        /// the full edge length, which made the section crop box visually trace the
        /// entire boundary edge instead of a short perpendicular marker. cropWidthMode
        /// is now UNUSED here (left as a parameter — still read by settings/UI JSON;
        /// not removed from RoofEdgeSectionsSettings without separate confirmation).
        /// </summary>
        private static BoundingBoxXYZ BuildSectionBoundingBox(
            PlannedSection row,
            double offsetFeet,
            double depthFeet,
            double cropHeightFeet,
            double fixedCropWidthFeet,
            string cropWidthMode)
        {
            XYZ viewDir = row.InwardNormal;               // looking inward, perpendicular to the edge
            XYZ upDir = XYZ.BasisZ;
            XYZ rightDir = viewDir.CrossProduct(upDir).Normalize();

            // Origin: edge midpoint, pulled back outward along -viewDir by the offset
            // so the section head sits clear of the roof edge rather than on top of it.
            XYZ origin = row.EdgeMidpoint - viewDir * offsetFeet;

            Transform t = Transform.Identity;
            t.Origin = origin;
            t.BasisX = rightDir;
            t.BasisY = upDir;
            t.BasisZ = viewDir;

            // Always a fixed short stub width — cropWidthMode/TightToEdgeSpan no longer
            // used for this dimension (see remarks above).
            double halfWidth = fixedCropWidthFeet / 2.0;

            var box = new BoundingBoxXYZ
            {
                Transform = t,
                Min = new XYZ(-halfWidth, 0, -offsetFeet),
                Max = new XYZ(halfWidth, cropHeightFeet, depthFeet)
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
