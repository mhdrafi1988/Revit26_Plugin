using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Revit26_Plugin.TaperedEpsPattern2D.V004.Core.Pattern
{
    /// <summary>
    /// Contour-row layout. On every slope face, rows are the bands between thickness contours
    /// (row n spans n−1 … n board widths from the edge; the last row at the ridge takes what is left).
    /// Where each row starts and where its leftover cut goes is set by <see cref="Pattern2DSettings.RowStart"/>:
    /// <list type="bullet">
    /// <item>A — from both hips, one filler in the middle; hip pieces mirror across every hip.</item>
    /// <item>B — from both hips, leftover split equally into the two hip ends (no middle cut).</item>
    /// <item>C — from the left hip, leftover at the right-hand hip (no middle cut).</item>
    /// <item>D — from the exact middle: D1 joint on the centre line, D2 board centred, or Auto per face.</item>
    /// </list>
    /// Board edges that are not on the face boundary become grid lines, joined along each line.
    /// </summary>
    public static class RowLayoutBuilder
    {
        private sealed class FaceLayout
        {
            public List<BoardPiece> Pieces { get; } = new List<BoardPiece>();
            public List<Segment2D> Edges { get; } = new List<Segment2D>();
            public int Slivers { get; set; }
            public int FullCount => Pieces.Count(p => p.Kind == PieceKind.Full);
        }

        public static RowLayoutResult Build(ThicknessField thickness, IReadOnlyList<SlopeFace> faces,
            Pattern2DSettings settings, double minLengthM)
        {
            Polygon2D outline = thickness.Distances.Outline;
            var boards = new List<BoardPiece>();
            var lines = new List<PatternSegment>();
            var notes = new List<string>();
            int slivers = 0;

            foreach (SlopeFace face in faces)
            {
                GridFrame frame = GridFrame.ForFace(face.Polygon, outline.Edge(face.EdgeIndex).Direction, settings);
                FaceLayout layout;

                if (settings.RowStart == RowStartMode.FromMiddle && settings.MiddleVariant == MiddleStartVariant.Auto)
                {
                    FaceLayout d1 = LayFace(face, frame, thickness, settings, MiddleStartVariant.JointOnCentre);
                    FaceLayout d2 = LayFace(face, frame, thickness, settings, MiddleStartVariant.BoardCentred);
                    bool pickD2 = d2.Slivers < d1.Slivers || (d2.Slivers == d1.Slivers && d2.FullCount > d1.FullCount);
                    layout = pickD2 ? d2 : d1;
                    notes.Add(string.Format(CultureInfo.InvariantCulture,
                        "Edge {0}: Auto chose {1} (D1: {2} full, {3} slivers · D2: {4} full, {5} slivers)",
                        face.EdgeIndex + 1, pickD2 ? "D2 board centred" : "D1 joint on centre line",
                        d1.FullCount, d1.Slivers, d2.FullCount, d2.Slivers));
                }
                else
                {
                    layout = LayFace(face, frame, thickness, settings, settings.MiddleVariant);
                }

                boards.AddRange(layout.Pieces);
                slivers += layout.Slivers;
                foreach (Segment2D s in CollinearUnion.Union(layout.Edges, GeometryTolerance.Merge))
                {
                    if (s.Length >= minLengthM)
                    {
                        lines.Add(new PatternSegment(PatternLayer.Grid, s, edgeIndex: face.EdgeIndex));
                    }
                }
            }

            return new RowLayoutResult
            {
                Boards = RowPieceCoder.Assign(boards),
                GridLines = lines,
                SliverCount = slivers,
                Notes = notes
            };
        }

        private static FaceLayout LayFace(SlopeFace face, GridFrame frame, ThicknessField thickness,
            Pattern2DSettings settings, MiddleStartVariant variant)
        {
            double l = settings.BoardLengthM;
            double w = settings.BoardWidthM;
            double vBase = frame.VMin; // the roof edge line
            Vec2 ToWorld(Vec2 q) => frame.ToWorld(q.X, q.Y + vBase);
            Polygon2D local = new Polygon2D(face.Polygon.Vertices.Select(p =>
            {
                Vec2 q = frame.ToLocal(p);
                return new Vec2(q.X, q.Y - vBase);
            }));
            double maxV = local.Vertices.Max(p => p.Y);
            var result = new FaceLayout();

            for (int row = 1; (row - 1) * w < maxV - GeometryTolerance.Vertex; row++)
            {
                double v0 = (row - 1) * w;
                double v1 = Math.Min(row * w, maxV);
                bool partialRow = v1 - v0 < w - GeometryTolerance.Vertex;

                Polygon2D strip = ConvexClipper.ClipAll(local, new[]
                {
                    new HalfPlane(new Vec2(0, 1), -v0),
                    new HalfPlane(new Vec2(0, -1), v1)
                });
                if (strip.IsEmpty || strip.Area < GeometryTolerance.MinLoopArea * 1e-2)
                {
                    continue;
                }

                if (ConvexClipper.ClipLine(local, new Vec2(0, v0), Vec2.UnitX) is not Segment2D outer)
                {
                    continue;
                }

                double a0 = Math.Min(outer.Start.X, outer.End.X);
                double b0 = Math.Max(outer.Start.X, outer.End.X);
                double mid = (a0 + b0) / 2;
                List<double> joints = RowJoints.For(settings.RowStart, variant, a0, b0, l, settings.MinPieceM);
                bool rowCanAvoidSlivers = b0 - a0 >= settings.MinPieceM;

                int cellCount = joints.Count - 1;
                for (int i = 0; i < cellCount; i++)
                {
                    var cuts = new List<HalfPlane>();
                    if (i > 0)
                    {
                        cuts.Add(new HalfPlane(Vec2.UnitX, -joints[i]));
                    }

                    if (i < cellCount - 1)
                    {
                        cuts.Add(new HalfPlane(-Vec2.UnitX, joints[i + 1]));
                    }

                    Polygon2D pieceLocal = ConvexClipper.ClipAll(strip, cuts);
                    if (pieceLocal.IsEmpty || pieceLocal.Area < GeometryTolerance.MinLoopArea * 1e-2)
                    {
                        continue;
                    }

                    PieceKind kind = Classify(pieceLocal, partialRow, l * w, cellCount, mid);
                    double minX = pieceLocal.Vertices.Min(p => p.X);
                    double maxX = pieceLocal.Vertices.Max(p => p.X);
                    if (kind != PieceKind.Full && rowCanAvoidSlivers && maxX - minX < settings.MinPieceM - GeometryTolerance.Vertex)
                    {
                        result.Slivers++;
                    }

                    double xRef = kind == PieceKind.HipLeft ? a0 : kind == PieceKind.HipRight ? b0 : minX;
                    Polygon2D world = new Polygon2D(pieceLocal.Vertices.Select(ToWorld)).ToCounterClockwise();
                    result.Pieces.Add(new BoardPiece
                    {
                        Id = string.Format(CultureInfo.InvariantCulture, "E{0}-R{1:00}-P{2:00}", face.EdgeIndex + 1, row, i + 1),
                        Row = row,
                        Column = i + 1,
                        Polygon = world,
                        IsFull = kind == PieceKind.Full,
                        Kind = kind,
                        EdgeIndex = face.EdgeIndex,
                        ShapeKey = ShapeKey(pieceLocal, xRef, v0, mirror: kind == PieceKind.HipRight),
                        MinThicknessMm = world.Vertices.Min(thickness.ThicknessAt),
                        MaxThicknessMm = world.Vertices.Max(thickness.ThicknessAt)
                    });

                    foreach (Segment2D e in world.Edges)
                    {
                        if (e.Length >= GeometryTolerance.Merge && !face.Polygon.IsOnBoundaryLine(e, GeometryTolerance.Merge))
                        {
                            result.Edges.Add(e);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Ridge row first; then full boards; a row with a single piece is a plain cut; any other piece
        /// that is not a rectangle has been cut by a hip (left or right half of the row); a rectangular
        /// part-board is a straight cut inside the row (the A-option filler).
        /// </summary>
        private static PieceKind Classify(Polygon2D piece, bool partialRow, double fullArea, int cellCount, double rowMid)
        {
            if (partialRow)
            {
                return PieceKind.RidgeRow;
            }

            if (Math.Abs(piece.Area - fullArea) < 1e-6)
            {
                return PieceKind.Full;
            }

            if (cellCount == 1)
            {
                return PieceKind.Cut;
            }

            (Vec2 min, Vec2 max) = piece.BoundingBox();
            bool rectangle = Math.Abs((max.X - min.X) * (max.Y - min.Y) - piece.Area) < 1e-6;
            if (rectangle)
            {
                return PieceKind.Filler;
            }

            return piece.Centroid.X < rowMid ? PieceKind.HipLeft : PieceKind.HipRight;
        }

        /// <summary>Vertices relative to the row (mm), mirrored for right-hand hip pieces, sorted.</summary>
        private static string ShapeKey(Polygon2D local, double xRef, double v0, bool mirror) =>
            string.Join(";", local.Vertices
                .Select(p => (X: (long)Math.Round((mirror ? xRef - p.X : p.X - xRef) * 1000), Y: (long)Math.Round((p.Y - v0) * 1000)))
                .Distinct()
                .OrderBy(p => p.X).ThenBy(p => p.Y)
                .Select(p => p.X.ToString(CultureInfo.InvariantCulture) + "," + p.Y.ToString(CultureInfo.InvariantCulture)));
    }
}
