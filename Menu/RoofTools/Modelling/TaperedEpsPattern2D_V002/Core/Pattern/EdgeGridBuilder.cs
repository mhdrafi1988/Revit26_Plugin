using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V002.Core.Pattern
{
    /// <summary>
    /// "Each edge" mode: every slope face gets its own board grid, parallel to the roof edge it
    /// drains to, with the first row flush on that edge. Lines are clipped to their own face, so
    /// each edge's grid stays a separate set of lines (tagged with the edge index).
    /// Board IDs are prefixed with the edge: "E1-R01-C01".
    /// </summary>
    public static class EdgeGridBuilder
    {
        public static EdgeGridSet Build(ThicknessField thickness, IReadOnlyList<SlopeFace> faces,
            Pattern2DSettings settings, double minLengthM)
        {
            Polygon2D outline = thickness.Distances.Outline;
            var lines = new List<PatternSegment>();
            var boards = new List<BoardPiece>();

            foreach (SlopeFace face in faces)
            {
                Vec2 edgeDirection = outline.Edge(face.EdgeIndex).Direction;
                GridFrame frame = GridFrame.ForFace(face.Polygon, edgeDirection, settings);

                // Grid lines on the face boundary (roof edge, hips, ridge) are not drawn: the roof
                // edge is the perimeter and hips/ridges are their own layer.
                foreach (Segment2D s in GridLineBuilder.Build(face.Polygon, frame, minLengthM))
                {
                    if (!lines.Any(l => l.Segment.SameAs(s, GeometryTolerance.Merge)))
                    {
                        lines.Add(new PatternSegment(PatternLayer.Grid, s, edgeIndex: face.EdgeIndex));
                    }
                }

                string prefix = string.Format(CultureInfo.InvariantCulture, "E{0}-", face.EdgeIndex + 1);
                boards.AddRange(BoardPieceBuilder.Build(frame, face.Polygon, thickness, faces, prefix, face.EdgeIndex));
            }

            return new EdgeGridSet { GridLines = lines, Boards = boards };
        }
    }
}
