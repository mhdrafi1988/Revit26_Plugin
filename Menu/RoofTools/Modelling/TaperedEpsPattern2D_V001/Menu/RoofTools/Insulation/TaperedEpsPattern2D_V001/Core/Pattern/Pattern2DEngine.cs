using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.TaperedEpsPattern2D.V001.Core.Pattern
{
    /// <summary>
    /// Builds the full 2D pattern for a validated convex CCW outline (metres).
    /// Pure geometry: no Revit, no WPF. Never throws for bad input — returns errors.
    /// </summary>
    public static class Pattern2DEngine
    {
        public static Pattern2DResult Generate(Polygon2D? outlineCcw, Pattern2DSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var errors = new List<string>();
            if (outlineCcw == null || outlineCcw.IsEmpty)
            {
                errors.Add("No valid roof outline.");
            }
            else if (!outlineCcw.IsCounterClockwise || !outlineCcw.IsConvex(GeometryTolerance.ParallelSine))
            {
                errors.Add("Roof outline must be convex and counter-clockwise (run OutlineValidator first).");
            }

            errors.AddRange(SettingsValidator.Validate(settings));
            if (errors.Count > 0)
            {
                return Pattern2DResult.Failed(errors);
            }

            Polygon2D outline = outlineCcw!;
            var distances = new EdgeDistanceField(outline);
            var thickness = new ThicknessField(distances, settings.MinThicknessMm, settings.SlopePercent);
            IReadOnlyList<SlopeFace> faces = SlopeFaceBuilder.Build(distances);

            double maxDistance = faces.SelectMany(f => f.Polygon.Vertices)
                                      .Select(distances.MinDistance)
                                      .DefaultIfEmpty(0)
                                      .Max();
            double maxThickness = settings.MinThicknessMm + thickness.K * Math.Max(0, maxDistance);

            GridFrame frame = GridFrame.Create(outline, settings);
            IReadOnlyList<BoardPiece> boards =
                BoardCodeAssigner.Assign(BoardPieceBuilder.Build(frame, thickness, faces));

            var segments = new List<PatternSegment>();
            double minLen = Math.Max(settings.MinSegmentLengthM, GeometryTolerance.Epsilon);

            if (settings.DrawGrid)
            {
                segments.AddRange(GridLineBuilder.Build(outline, frame, minLen)
                    .Select(s => new PatternSegment(PatternLayer.Grid, s)));
            }

            if (settings.DrawHipRidge)
            {
                segments.AddRange(HipRidgeExtractor.Extract(outline, faces, minLen)
                    .Select(s => new PatternSegment(PatternLayer.HipRidge, s)));
            }

            IReadOnlyList<ContourLoop> contours = settings.DrawContours
                ? ContourBuilder.Build(thickness, settings.ContourIntervalMm, maxThickness)
                : Array.Empty<ContourLoop>();
            foreach (ContourLoop loop in contours)
            {
                segments.AddRange(loop.Polygon.Edges
                    .Where(e => e.Length >= minLen)
                    .Select(e => new PatternSegment(PatternLayer.Contour, e, loop.ThicknessMm)));
            }

            return new Pattern2DResult
            {
                Outline = outline,
                Frame = frame,
                Faces = faces,
                Boards = boards,
                Contours = contours,
                Segments = segments,
                Labels = BoardLabelBuilder.Build(boards, settings.Labels),
                MaxThicknessMm = maxThickness
            };
        }
    }
}
