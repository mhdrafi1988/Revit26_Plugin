using Autodesk.Revit.DB;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.Helpers
{
    /// <summary>
    /// Orchestrates Steps 2, 3, 5 and 6 from the spec: collect annotations,
    /// group by type, detect overlapping pairs, and compute grid alignment
    /// and distance metrics for each pair. Read-only - nothing here modifies the model.
    /// </summary>
    internal static class OverlapDetectionHelper
    {
        private const double AlignmentToleranceMm = 0.1;

        private static readonly Type[] AnnotationTypes =
        {
            typeof(TextNote),
            typeof(IndependentTag),
            typeof(SpatialElementTag),
            typeof(Dimension)
        };

        private static readonly HashSet<ViewType> SupportedViewTypes = new()
        {
            ViewType.FloorPlan,
            ViewType.CeilingPlan,
            ViewType.AreaPlan,
            ViewType.EngineeringPlan,
            ViewType.Section,
            ViewType.Elevation,
            ViewType.Detail,
            ViewType.DraftingView,
            ViewType.Legend
        };

        /// <summary>
        /// True for the 2D view types the check supports (plans, sections, elevations,
        /// detail, drafting and legend views). Templates and 3D / sheet / schedule views are rejected.
        /// </summary>
        public static bool IsSupportedView(View view)
        {
            return view != null
                && view.IsValidObject
                && !view.IsTemplate
                && SupportedViewTypes.Contains(view.ViewType);
        }

        /// <summary>
        /// Step 2: collect all annotation elements visible in the active view.
        /// </summary>
        public static List<AnnotationData> GetAnnotationsFromView(View view, Document doc)
        {
            var results = new List<AnnotationData>();

            foreach (Type type in AnnotationTypes)
            {
                var collector = new FilteredElementCollector(doc, view.Id)
                    .OfClass(type)
                    .WhereElementIsNotElementType();

                foreach (Element elem in collector)
                {
                    var box = BoundingBoxCalculator.GetBoundingBox(elem, view);
                    if (box == null)
                        continue;

                    results.Add(new AnnotationData
                    {
                        ElementId = elem.Id,
                        TypeName = type.Name,
                        X = box.Value.x,
                        Y = box.Value.y,
                        Width = box.Value.width,
                        Height = box.Value.height
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Step 3: group collected annotations by type name for the checkbox list.
        /// </summary>
        public static Dictionary<string, List<AnnotationData>> GroupByType(List<AnnotationData> annotations)
        {
            return annotations
                .GroupBy(a => a.TypeName)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Step 5 + 6: compare every pair once (A,B) not (B,A), skip self-pairs,
        /// and compute metrics for overlaps.
        /// </summary>
        /// <param name="annotations">Annotations of the selected types.</param>
        /// <param name="crossType">
        /// false (default, v4.0 behaviour): only pairs of the same type are compared.
        /// true: every pair is compared, e.g. a tag sitting on a text note.
        /// </param>
        /// <param name="viewScale">View scale (e.g. 100 for 1:100); used for paper mm. Values below 1 count as 1.</param>
        /// <param name="minOverlapPaperMm">
        /// Ignore overlaps whose width or height on paper is below this value. 0 (default) reports every overlap.
        /// </param>
        public static List<OverlapResult> DetectOverlaps(List<AnnotationData> annotations, bool crossType,
            int viewScale, double minOverlapPaperMm)
        {
            var results = new List<OverlapResult>();
            if (annotations == null || annotations.Count < 2)
                return results;

            double scale = Math.Max(1, viewScale);
            double minPaper = Math.Max(0, minOverlapPaperMm);

            // Sort by X so the inner loop can stop as soon as boxes can no longer overlap.
            var sorted = annotations.OrderBy(a => a.X).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                AnnotationData a = sorted[i];
                for (int j = i + 1; j < sorted.Count; j++)
                {
                    AnnotationData b = sorted[j];
                    if (b.X >= a.Right)
                        break; // all later boxes start further right

                    if (a.ElementId == b.ElementId)
                        continue; // edge case 7: exclude self-overlap

                    if (!crossType && a.TypeName != b.TypeName)
                        continue;

                    if (!BoundingBoxCalculator.DoBoxesIntersect(a, b))
                        continue;

                    var (ow, oh) = BoundingBoxCalculator.GetOverlapSize(a, b);
                    double owPaper = ow / scale;
                    double ohPaper = oh / scale;
                    if (owPaper < minPaper || ohPaper < minPaper)
                        continue;

                    // Keep the lower id first so results read the same every run.
                    var (first, second) = a.ElementId.Value <= b.ElementId.Value ? (a, b) : (b, a);

                    bool aligned = CalculateGridAlignment(first, second);
                    var (vGap, hGap) = BoundingBoxCalculator.CalculateGap(first, second);

                    results.Add(new OverlapResult
                    {
                        ElementId1 = first.ElementId.Value,
                        ElementId2 = second.ElementId.Value,
                        AnnotationType = first.TypeName == second.TypeName ? first.TypeName : $"{first.TypeName} / {second.TypeName}",
                        X = Math.Round(first.X, 4),
                        Y = Math.Round(first.Y, 4),
                        GridAligned = aligned ? "Yes" : "No",
                        VerticalDistance = vGap,
                        HorizontalDistance = hGap,
                        OverlapWidthPaper = Math.Round(owPaper, 2),
                        OverlapHeightPaper = Math.Round(ohPaper, 2)
                    });
                }
            }

            return results
                .OrderBy(r => r.AnnotationType)
                .ThenBy(r => r.ElementId1)
                .ThenBy(r => r.ElementId2)
                .ToList();
        }

        /// <summary>
        /// Step 6: "Yes" if X OR Y insertion coordinates match within a
        /// 0.1mm tolerance (edge case 6), "No" otherwise.
        /// </summary>
        public static bool CalculateGridAlignment(AnnotationData a, AnnotationData b)
        {
            bool xAligned = Math.Abs(a.X - b.X) <= AlignmentToleranceMm;
            bool yAligned = Math.Abs(a.Y - b.Y) <= AlignmentToleranceMm;
            return xAligned || yAligned;
        }
    }
}
