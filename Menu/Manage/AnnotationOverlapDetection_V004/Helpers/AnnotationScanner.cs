using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Models;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.Helpers
{
    /// <summary>
    /// v4.2.0: collects annotations from one or more views with tighter outlines:
    /// rotated text and tags get a rotated rectangle, and leaders can be left out.
    /// Must run in a Revit API context. The model is never changed: leaders are removed
    /// inside a transaction that is always rolled back, only to measure the boxes.
    /// </summary>
    internal static class AnnotationScanner
    {
        private const double FeetToMm = 304.8;

        private static readonly Type[] AnnotationTypes =
        {
            typeof(TextNote),
            typeof(IndependentTag),
            typeof(SpatialElementTag),
            typeof(Dimension)
        };

        /// <summary>Result of a scan: the annotations plus any warnings for the user.</summary>
        public sealed class ScanResult
        {
            /// <summary>Annotations found in all scanned views.</summary>
            public List<AnnotationData> Annotations { get; } = new();

            /// <summary>Non-fatal problems, e.g. a view whose leaders could not be left out.</summary>
            public List<string> Warnings { get; } = new();
        }

        /// <summary>
        /// Scans the given views. Views that are invalid or unsupported are skipped with a warning.
        /// </summary>
        /// <param name="includeLeaders">
        /// true: boxes include leaders (v4.1 behaviour). false: leaders are left out so only the
        /// tag head / text box is checked.
        /// </param>
        public static ScanResult Scan(Document doc, IEnumerable<View> views, bool includeLeaders)
        {
            var result = new ScanResult();
            foreach (View view in views)
            {
                if (!OverlapDetectionHelper.IsSupportedView(view))
                {
                    result.Warnings.Add($"Skipped \"{view?.Name}\": not a supported 2D view.");
                    continue;
                }

                try
                {
                    ScanView(doc, view, includeLeaders, result);
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Skipped \"{view.Name}\": {ex.Message}");
                }
            }
            return result;
        }

        private static void ScanView(Document doc, View view, bool includeLeaders, ScanResult result)
        {
            var elements = new List<(Element elem, string typeName)>();
            foreach (Type type in AnnotationTypes)
            {
                foreach (Element e in new FilteredElementCollector(doc, view.Id).OfClass(type).WhereElementIsNotElementType())
                    elements.Add((e, type.Name));
            }
            if (elements.Count == 0)
                return;

            bool needsLeaderRemoval = !includeLeaders && elements.Any(x => HasLeader(x.elem));

            if (!needsLeaderRemoval)
            {
                Measure(view, elements, result.Annotations);
                return;
            }

            if (doc.IsReadOnly || doc.IsModifiable)
            {
                result.Warnings.Add($"\"{view.Name}\": leaders could not be left out (document is read-only or busy); boxes include leaders.");
                Measure(view, elements, result.Annotations);
                return;
            }

            // Remove leaders only to measure, then always roll back - nothing is saved.
            using var t = new Transaction(doc, "Measure annotations without leaders");
            try
            {
                var opts = t.GetFailureHandlingOptions();
                opts.SetFailuresPreprocessor(new SwallowWarnings());
                opts.SetClearAfterRollback(true);
                t.SetFailureHandlingOptions(opts);

                if (t.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("could not start transaction");

                int failed = 0;
                foreach (var (elem, _) in elements)
                {
                    if (!TryRemoveLeader(elem))
                        failed++;
                }
                doc.Regenerate();

                Measure(view, elements, result.Annotations);

                if (failed > 0)
                    result.Warnings.Add($"\"{view.Name}\": {failed} element(s) kept their leader in the check (not editable).");
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"\"{view.Name}\": leaders could not be left out ({ex.Message}); boxes include leaders.");
                if (t.HasStarted() && !t.HasEnded())
                    t.RollBack();
                result.Annotations.RemoveAll(a => a.ViewId == view.Id);
                Measure(view, elements, result.Annotations);
                return;
            }
            finally
            {
                if (t.HasStarted() && !t.HasEnded())
                    t.RollBack();
            }
        }

        private static void Measure(View view, List<(Element elem, string typeName)> elements, List<AnnotationData> output)
        {
            int scale = Math.Max(1, view.Scale);
            foreach (var (elem, typeName) in elements)
            {
                var box = BoundingBoxCalculator.GetBoundingBox(elem, view);
                if (box == null)
                    continue;

                var (x, y, w, h) = box.Value;
                output.Add(new AnnotationData
                {
                    ElementId = elem.Id,
                    TypeName = typeName,
                    ViewId = view.Id,
                    ViewName = view.Name,
                    ViewScale = scale,
                    X = x,
                    Y = y,
                    Width = w,
                    Height = h,
                    Outline = RotatedOutline(elem, view, scale, x, y, w, h)
                });
            }
        }

        /// <summary>Rotated rectangle for rotated text / tags; null keeps the axis-aligned box.</summary>
        private static IReadOnlyList<Pt2> RotatedOutline(Element elem, View view, int scale,
            double x, double y, double w, double h)
        {
            try
            {
                switch (elem)
                {
                    case TextNote text:
                    {
                        XYZ dir = text.BaseDirection;
                        double angle = Math.Atan2(dir.DotProduct(view.UpDirection), dir.DotProduct(view.RightDirection));
                        double knownWidth = text.Width * scale * FeetToMm;
                        return PolygonMath.RotatedRectangleInBox(x, y, w, h, angle, knownWidth);
                    }
                    case IndependentTag tag:
                    {
                        double angle = tag.RotationAngle;
                        if (tag.TagOrientation == TagOrientation.Vertical && Math.Abs(angle) < 1e-6)
                            angle = Math.PI / 2;
                        return PolygonMath.RotatedRectangleInBox(x, y, w, h, angle);
                    }
                    case SpatialElementTag roomTag:
                        return PolygonMath.RotatedRectangleInBox(x, y, w, h, roomTag.RotationAngle);
                    default:
                        return null; // dimensions: whole box, as before
                }
            }
            catch (Exception)
            {
                return null; // any API surprise: fall back to the axis-aligned box
            }
        }

        private static bool HasLeader(Element elem)
        {
            try
            {
                return elem switch
                {
                    IndependentTag tag => tag.HasLeader,
                    SpatialElementTag roomTag => roomTag.HasLeader,
                    TextNote text => text.LeaderCount > 0,
                    _ => false
                };
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryRemoveLeader(Element elem)
        {
            try
            {
                switch (elem)
                {
                    case IndependentTag tag when tag.HasLeader:
                        tag.HasLeader = false;
                        break;
                    case SpatialElementTag roomTag when roomTag.HasLeader:
                        roomTag.HasLeader = false;
                        break;
                    case TextNote text when text.LeaderCount > 0:
                        text.RemoveLeaders();
                        break;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Deletes warnings raised while measuring so no dialog appears; the transaction is rolled back anyway.</summary>
        private sealed class SwallowWarnings : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
            {
                accessor.DeleteAllWarnings();
                return FailureProcessingResult.Continue;
            }
        }
    }
}
