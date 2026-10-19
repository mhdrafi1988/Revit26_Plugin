using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin_Revised.TaperedEpsPattern2D.V002.Core.Pattern;

namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V002.Revit
{
    /// <summary>
    /// Writes the pattern as detail lines (+ optional text notes and detail groups) in the view
    /// where the roof was picked. ONE transaction — any failure rolls back everything.
    /// Must run in a valid API context (ExternalEvent handler).
    /// </summary>
    public static class DetailPatternWriter
    {
        public const string TransactionName = "Tapered EPS 2D Pattern";

        public static PatternWriteResult Write(PatternWriteRequest request)
        {
            RoofContext ctx = request.Roof;
            Document doc = ctx.Document;

            if (!doc.IsValidObject)
            {
                return PatternWriteResult.Failed("The document was closed.");
            }

            if (doc.GetElement(ctx.ViewId) is not ViewPlan view || doc.GetElement(ctx.RoofId) is not FootPrintRoof)
            {
                return PatternWriteResult.Failed("The roof or the view no longer exists. Close the tool and run it again.");
            }

            if (!request.Pattern.IsValid)
            {
                return PatternWriteResult.Failed("The pattern is not valid: " + string.Join(" ", request.Pattern.Errors));
            }

            double z = ctx.LevelElevationFeet;
            double shortTol = Math.Max(ctx.ShortCurveToleranceFeet, doc.Application.ShortCurveTolerance);
            var created = new List<ElementId>();
            var buckets = new Dictionary<string, List<ElementId>>();
            var perLayer = new Dictionary<PatternLayer, int>();
            var warnings = new List<string>();
            int skipped = 0, textNotes = 0, replaced = 0;
            var groupNames = new List<string>();
            HashSet<ElementId>? allowedStyles = null;

            var swallower = new WarningSwallower();
            using (var t = new Transaction(doc, TransactionName))
            {
                FailureHandlingOptions failureOptions = t.GetFailureHandlingOptions();
                failureOptions.SetFailuresPreprocessor(swallower);
                failureOptions.SetClearAfterRollback(true);
                t.SetFailureHandlingOptions(failureOptions);
                t.Start();
                try
                {
                    if (request.Grouping != GroupingMode.NoGroup && request.ReplacePrevious)
                    {
                        replaced = DetailGroupManager.DeleteExisting(doc, ctx.GroupName);
                    }

                    foreach (PatternSegment seg in request.Pattern.Segments)
                    {
                        XYZ a = UnitConverter.ToModel(seg.Segment.Start, z);
                        XYZ b = UnitConverter.ToModel(seg.Segment.End, z);
                        if (a.DistanceTo(b) < shortTol)
                        {
                            skipped++;
                            continue;
                        }

                        DetailCurve curve = doc.Create.NewDetailCurve(view, Line.CreateBound(a, b));
                        allowedStyles ??= new HashSet<ElementId>(curve.GetLineStyleIds());

                        if (request.LineStyles.TryGetValue(seg.Layer, out ElementId? styleId) &&
                            styleId != null && allowedStyles.Contains(styleId) &&
                            doc.GetElement(styleId) is GraphicsStyle style)
                        {
                            curve.LineStyle = style;
                        }
                        else if (!warnings.Contains(seg.Layer.ToString()))
                        {
                            warnings.Add(seg.Layer.ToString());
                        }

                        created.Add(curve.Id);
                        string key = seg.Layer == PatternLayer.Grid && seg.EdgeIndex.HasValue
                            ? $"Grid_E{seg.EdgeIndex.Value + 1}"
                            : LayerSuffix(seg.Layer);
                        Bucket(buckets, key).Add(curve.Id);
                        perLayer[seg.Layer] = perLayer.TryGetValue(seg.Layer, out int n) ? n + 1 : 1;
                    }

                    if (request.Pattern.Labels.Count > 0 && request.TextNoteTypeId != null &&
                        request.TextNoteTypeId != ElementId.InvalidElementId)
                    {
                        var options = new TextNoteOptions(request.TextNoteTypeId)
                        {
                            HorizontalAlignment = HorizontalTextAlignment.Center,
                            VerticalAlignment = VerticalTextAlignment.Middle
                        };

                        foreach (PatternLabel label in request.Pattern.Labels)
                        {
                            TextNote note = TextNote.Create(doc, view.Id, UnitConverter.ToModel(label.Position, z), label.Text, options);
                            created.Add(note.Id);
                            Bucket(buckets, "Labels").Add(note.Id);
                            textNotes++;
                        }
                    }

                    if (created.Count == 0)
                    {
                        t.RollBack();
                        return PatternWriteResult.Failed("Nothing to create — every segment was shorter than Revit's short-curve tolerance.");
                    }

                    if (request.Grouping == GroupingMode.SingleGroup)
                    {
                        groupNames.Add(DetailGroupManager.Create(doc, created, ctx.GroupName).GroupType.Name);
                    }
                    else if (request.Grouping == GroupingMode.GroupPerLayer)
                    {
                        foreach (KeyValuePair<string, List<ElementId>> bucket in buckets)
                        {
                            groupNames.Add(DetailGroupManager.Create(doc, bucket.Value, $"{ctx.GroupName}_{bucket.Key}").GroupType.Name);
                        }
                    }

                    if (t.Commit() != TransactionStatus.Committed)
                    {
                        return PatternWriteResult.Failed("Revit did not commit the transaction; nothing was created.");
                    }
                }
                catch (Exception ex)
                {
                    if (t.GetStatus() == TransactionStatus.Started)
                    {
                        t.RollBack();
                    }

                    return PatternWriteResult.Failed("Creation failed and was rolled back: " + ex.Message);
                }
            }

            return new PatternWriteResult
            {
                Success = true,
                LinesCreated = perLayer.Values.Sum(),
                TextNotesCreated = textNotes,
                SkippedShortSegments = skipped,
                ReplacedGroups = replaced,
                GroupNames = groupNames,
                LinesPerLayer = perLayer,
                Warnings = warnings
                    .Select(l => $"Line style for layer {l} is not usable on detail lines; Revit's default style was kept.")
                    .Concat(swallower.Messages.Select(m => "Revit warning (suppressed): " + m))
                    .ToList(),
                Message = $"{perLayer.Values.Sum()} lines created" +
                          (textNotes > 0 ? $", {textNotes} labels" : string.Empty) +
                          (groupNames.Count == 1 ? $" in group {groupNames[0]}" :
                           groupNames.Count > 1 ? $" in {groupNames.Count} groups ({string.Join(", ", groupNames)})" :
                           " as loose detail lines") + "."
            };
        }

        private static string LayerSuffix(PatternLayer layer) => layer switch
        {
            PatternLayer.Grid => "Grid",
            PatternLayer.HipRidge => "HipRidge",
            PatternLayer.Contour => "Contours",
            _ => layer.ToString()
        };

        private static List<ElementId> Bucket(Dictionary<string, List<ElementId>> buckets, string key)
        {
            if (!buckets.TryGetValue(key, out List<ElementId>? list))
            {
                list = new List<ElementId>();
                buckets[key] = list;
            }

            return list;
        }
    }
}
