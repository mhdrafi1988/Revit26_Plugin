using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.LinkedDetailLineGenerator.VA007.Core.Models;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA007.Core.Services
{
    public enum ColumnFootprintShape { Circle, Rectangle, Polygon }

    /// <summary>Where a footprint's shape and size came from, best to worst.</summary>
    public enum ColumnFootprintSource { Geometry, Parameters, FixedMarker }

    /// <summary>A column's real plan footprint, in the LINKED document's coordinate
    /// system (transform to host happens in the engine, same as every other
    /// extraction service). Curves are ready to draw: two half-arcs for a circle,
    /// four clean lines for a rectangle, the raw outer loop for anything else.</summary>
    public class ColumnFootprint
    {
        public ColumnFootprintShape Shape { get; set; }
        public ColumnFootprintSource Source { get; set; } = ColumnFootprintSource.Geometry;
        public XYZ Center { get; set; } = XYZ.Zero;
        public List<Curve> Curves { get; set; } = new();

        /// <summary>Circle only (feet).</summary>
        public double DiameterFeet { get; set; }

        /// <summary>Rectangle only (feet). Width runs along the rotated X axis and
        /// is the longer side; Height is the shorter one.</summary>
        public double WidthFeet { get; set; }
        public double HeightFeet { get; set; }

        /// <summary>Rectangle only. Angle of the Width axis in the linked document,
        /// normalised to (-90°, 90°] so a rectangle has exactly one rotation value.
        /// 0 = not rotated.</summary>
        public double RotationRadians { get; set; }

        /// <summary>True when the footprint came from a down-facing (bottom) face —
        /// the clean one; false means only a top face was usable.</summary>
        public bool FromBottomFace { get; set; }
    }

    /// <summary>
    /// Reads a column's plan footprint from its solid geometry instead of using a
    /// fixed marker size. The column's own geometry already carries its rotation,
    /// mirroring and offset, so a rotated column needs no separate rotation step:
    ///
    ///   1. Collect every horizontal planar face of every solid (family instance
    ///      geometry is expanded through GetInstanceGeometry, which returns it in
    ///      the linked document's coordinates).
    ///   2. Down-facing (bottom) faces are tried first, largest first: the bottom
    ///      is not trimmed by beams/slabs joined above, so it is the clean outline.
    ///      Top faces are tried only if no bottom face classifies cleanly.
    ///   3. The face's largest loop is classified:
    ///        - one full circle   → Circle (centre + diameter)
    ///        - four right-angled lines (collinear splits merged) → Rectangle
    ///          (size + rotation)
    ///        - anything else (I/L/T sections, rounded corners…) → Polygon, the
    ///          loop is used as-is.
    ///   The first Circle/Rectangle wins; otherwise the best Polygon is returned.
    ///
    /// Limitations (by design, documented rather than guessed): only the outer
    /// loop is used, so a hollow section is drawn solid; a stepped column resolves
    /// to its largest bottom face (e.g. a base plate rather than the shaft).
    ///
    /// When geometry gives nothing, TryExtractFromParameters reads the family's own
    /// size parameters, and BuildFixedFootprint is the last resort — both aligned to
    /// the element's own rotation (GetOwnRotation) so the fallback still lines up
    /// with the real column.
    /// </summary>
    public class ColumnFootprintService
    {
        private const double HorizontalNormalZ = 0.999;   // |nz| above this = horizontal
        private const double LengthTolFeet = 1e-3;         // ~0.3 mm
        private const double PerpendicularTol = 1e-3;      // |cos| between adjacent sides (~0.06°)
        private const double ParallelTol = 1e-6;           // |sin| between collinear segments
        private const double FullCircleTol = 1e-3;         // radians short of 2π

        private readonly PointMarkerGeometryService _markerGeometry = new();

        public bool TryExtract(Element element, out ColumnFootprint? footprint, out string? failureReason)
        {
            footprint = null;
            failureReason = null;

            GeometryElement? geomElem;
            try
            {
                geomElem = element.get_Geometry(new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine });
            }
            catch (Exception ex)
            {
                failureReason = $"geometry extraction failed: {ex.Message}";
                return false;
            }

            if (geomElem == null)
            {
                failureReason = "no geometry returned for element";
                return false;
            }

            var faces = new List<PlanarFace>();
            CollectHorizontalFaces(geomElem, faces);
            if (faces.Count == 0)
            {
                failureReason = "no horizontal planar face found";
                return false;
            }

            ColumnFootprint? bestPolygon = null;

            foreach (PlanarFace face in faces
                .OrderByDescending(f => f.FaceNormal.Z < 0)
                .ThenByDescending(f => f.Area))
            {
                ColumnFootprint? candidate = BuildFootprint(face);
                if (candidate == null) continue;

                if (candidate.Shape != ColumnFootprintShape.Polygon)
                {
                    footprint = candidate;
                    return true;
                }

                bestPolygon ??= candidate;
            }

            footprint = bestPolygon;
            if (footprint == null)
                failureReason = "no usable boundary loop on any horizontal face";
            return footprint != null;
        }

        private static void CollectHorizontalFaces(GeometryElement geometry, List<PlanarFace> faces)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid && solid.Volume > 1e-9)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace planar && Math.Abs(planar.FaceNormal.Normalize().Z) > HorizontalNormalZ)
                            faces.Add(planar);
                    }
                }
                else if (obj is GeometryInstance instance)
                {
                    CollectHorizontalFaces(instance.GetInstanceGeometry(), faces);
                }
            }
        }

        private ColumnFootprint? BuildFootprint(PlanarFace face)
        {
            List<Curve>? outer = GetOuterLoop(face);
            if (outer == null || outer.Count == 0) return null;

            ColumnFootprint footprint =
                TryCircle(outer) ?? TryRectangle(outer) ?? BuildPolygon(outer);
            footprint.FromBottomFace = face.FaceNormal.Z < 0;
            return footprint;
        }

        /// <summary>Largest-area loop of the face, each edge oriented along the loop
        /// (AsCurveFollowingFace) so consecutive curves join head-to-tail.</summary>
        private static List<Curve>? GetOuterLoop(Face face)
        {
            List<Curve>? best = null;
            double bestArea = -1;

            foreach (EdgeArray edgeArray in face.EdgeLoops)
            {
                var loop = new List<Curve>();
                foreach (Edge edge in edgeArray)
                    loop.Add(edge.AsCurveFollowingFace(face));

                double area = Math.Abs(LoopArea(loop));
                if (area > bestArea)
                {
                    bestArea = area;
                    best = loop;
                }
            }

            return best;
        }

        /// <summary>Shoelace area over the tessellated loop — used only to rank
        /// loops (outer = largest), never for geometry output.</summary>
        private static double LoopArea(List<Curve> loop)
        {
            var points = new List<XYZ>();
            foreach (Curve c in loop)
            {
                IList<XYZ> tess = c.Tessellate();
                for (int i = 0; i < tess.Count - 1; i++)
                    points.Add(tess[i]);
            }

            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                XYZ p1 = points[i];
                XYZ p2 = points[(i + 1) % points.Count];
                area += p1.X * p2.Y - p2.X * p1.Y;
            }
            return area / 2.0;
        }

        // ── Circle ──────────────────────────────────────────────────────────

        /// <summary>All edges are arcs on the same centre and radius and together
        /// sweep a full turn. Revit may return one closed arc or several pieces.</summary>
        private ColumnFootprint? TryCircle(List<Curve> loop)
        {
            if (loop.Any(c => c is not Arc)) return null;

            Arc first = (Arc)loop[0];
            XYZ center = first.Center;
            double radius = first.Radius;

            double sweep = 0;
            foreach (Arc arc in loop.Cast<Arc>())
            {
                if (arc.Center.DistanceTo(center) > LengthTolFeet || Math.Abs(arc.Radius - radius) > LengthTolFeet)
                    return null;
                sweep += arc.Length / radius;
            }

            if (Math.Abs(sweep - 2 * Math.PI) > FullCircleTol) return null;

            return new ColumnFootprint
            {
                Shape = ColumnFootprintShape.Circle,
                Center = center,
                DiameterFeet = radius * 2,
                Curves = _markerGeometry.BuildCircleFromRadius(center, radius)
            };
        }

        // ── Rectangle ───────────────────────────────────────────────────────

        private static ColumnFootprint? TryRectangle(List<Curve> loop)
        {
            List<Line>? sides = MergeCollinearLines(loop);
            if (sides == null || sides.Count != 4) return null;

            var dirs = new XYZ[4];
            var lengths = new double[4];
            for (int i = 0; i < 4; i++)
            {
                lengths[i] = sides[i].Length;
                dirs[i] = sides[i].Direction.Normalize();
            }

            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                if (Math.Abs(dirs[i].DotProduct(dirs[next])) > PerpendicularTol) return null;
                if (sides[i].GetEndPoint(1).DistanceTo(sides[next].GetEndPoint(0)) > LengthTolFeet) return null;
            }

            if (Math.Abs(lengths[0] - lengths[2]) > LengthTolFeet
                || Math.Abs(lengths[1] - lengths[3]) > LengthTolFeet)
                return null;

            // Rebuild from the four corners so tiny gaps in the source loop vanish.
            XYZ[] corners = sides.Select(s => s.GetEndPoint(0)).ToArray();
            var curves = new List<Curve>(4);
            for (int i = 0; i < 4; i++)
                curves.Add(Line.CreateBound(corners[i], corners[(i + 1) % 4]));

            int longest = lengths[0] >= lengths[1] ? 0 : 1;
            int shortest = 1 - longest;

            return new ColumnFootprint
            {
                Shape = ColumnFootprintShape.Rectangle,
                Center = new XYZ(corners.Average(c => c.X), corners.Average(c => c.Y), corners.Average(c => c.Z)),
                WidthFeet = lengths[longest],
                HeightFeet = lengths[shortest],
                RotationRadians = NormalizeAxisAngle(Math.Atan2(dirs[longest].Y, dirs[longest].X)),
                Curves = curves
            };
        }

        /// <summary>Consecutive same-direction Lines are joined into one (a rectangle
        /// side split into two edges is still one side). Returns null if the loop has
        /// anything other than Lines.</summary>
        private static List<Line>? MergeCollinearLines(List<Curve> loop)
        {
            if (loop.Any(c => c is not Line)) return null;

            var merged = new List<Line>();
            foreach (Line line in loop.Cast<Line>())
            {
                if (merged.Count > 0 && SameDirection(merged[^1], line))
                    merged[^1] = Line.CreateBound(merged[^1].GetEndPoint(0), line.GetEndPoint(1));
                else
                    merged.Add(line);
            }

            // The loop's last and first edge may belong to the same side.
            if (merged.Count > 1 && SameDirection(merged[^1], merged[0]))
            {
                Line joined = Line.CreateBound(merged[^1].GetEndPoint(0), merged[0].GetEndPoint(1));
                merged.RemoveAt(merged.Count - 1);
                merged[0] = joined;
            }

            return merged;
        }

        private static bool SameDirection(Line a, Line b)
        {
            XYZ da = a.Direction.Normalize();
            XYZ db = b.Direction.Normalize();
            return da.CrossProduct(db).GetLength() < ParallelTol && da.DotProduct(db) > 0;
        }

        /// <summary>Maps any angle into (-π/2, π/2] — a rectangle looks identical after
        /// a half turn, so this gives it a single canonical rotation.</summary>
        private static double NormalizeAxisAngle(double radians)
        {
            while (radians > Math.PI / 2) radians -= Math.PI;
            while (radians <= -Math.PI / 2) radians += Math.PI;
            return radians;
        }

        // ── Fallback 1: family parameters ───────────────────────────────────

        // Case-insensitive names tried in order. Steel sections (bf/d/tf…) are
        // deliberately not guessed here — their outline comes from geometry.
        private static readonly string[] DiameterNames = { "Diameter", "Column Diameter", "Dia" };

        private static readonly (string Width, string Height)[] RectangleNames =
        {
            ("b", "h"),
            ("Width", "Depth"),
            ("Width", "Height"),
            ("Column Width", "Column Depth"),
            ("Breadth", "Depth"),
        };

        /// <summary>Circle from a Diameter parameter, or rectangle from a b/h-style
        /// pair, centred on the insertion point and rotated to the element's own
        /// rotation. Instance parameters win over type parameters. The Width/b
        /// parameter is assumed to run along the family's local X axis.</summary>
        public bool TryExtractFromParameters(Element element, XYZ insertionPoint, out ColumnFootprint? footprint)
        {
            footprint = null;

            Dictionary<string, double> lengths = ReadLengthParameters(element);

            foreach (string name in DiameterNames)
            {
                if (!lengths.TryGetValue(name, out double diameter)) continue;

                footprint = new ColumnFootprint
                {
                    Shape = ColumnFootprintShape.Circle,
                    Source = ColumnFootprintSource.Parameters,
                    Center = insertionPoint,
                    DiameterFeet = diameter,
                    Curves = _markerGeometry.BuildCircleFromRadius(insertionPoint, diameter / 2.0)
                };
                return true;
            }

            foreach ((string widthName, string heightName) in RectangleNames)
            {
                if (!lengths.TryGetValue(widthName, out double width) || !lengths.TryGetValue(heightName, out double height))
                    continue;

                footprint = BuildRectangleFootprint(insertionPoint, width, height, GetOwnRotation(element), ColumnFootprintSource.Parameters);
                return true;
            }

            return false;
        }

        /// <summary>Positive length-valued parameters of the instance and its type,
        /// keyed case-insensitively; instance values override type values.</summary>
        private static Dictionary<string, double> ReadLengthParameters(Element element)
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            void AddFrom(Element? source)
            {
                if (source == null) return;
                foreach (Parameter p in source.Parameters)
                {
                    if (p.StorageType != StorageType.Double || !p.HasValue) continue;
                    if (p.Definition == null || p.Definition.GetDataType() != SpecTypeId.Length) continue;

                    double value = p.AsDouble();
                    if (value > 1e-6) result[p.Definition.Name] = value;
                }
            }

            AddFrom(element.Document.GetElement(element.GetTypeId()));
            AddFrom(element);
            return result;
        }

        // ── Fallback 2: fixed-size marker ───────────────────────────────────

        /// <summary>Last resort: the configured fixed-size Circle/Rectangle marker at
        /// the insertion point. A Rectangle is still rotated to the element's own
        /// rotation, regardless of the Alignment setting.</summary>
        public ColumnFootprint BuildFixedFootprint(
            Element element, XYZ insertionPoint, PointMarkerShape shape,
            CircleMarkerSettings circle, RectangleMarkerSettings rectangle)
        {
            if (shape == PointMarkerShape.Rectangle)
                return BuildRectangleFootprint(
                    insertionPoint, MmToFeet(rectangle.WidthMm), MmToFeet(rectangle.HeightMm),
                    GetOwnRotation(element), ColumnFootprintSource.FixedMarker);

            double diameter = MmToFeet(circle.DiameterMm);
            return new ColumnFootprint
            {
                Shape = ColumnFootprintShape.Circle,
                Source = ColumnFootprintSource.FixedMarker,
                Center = insertionPoint,
                DiameterFeet = diameter,
                Curves = _markerGeometry.BuildCircleFromRadius(insertionPoint, diameter / 2.0)
            };
        }

        private ColumnFootprint BuildRectangleFootprint(
            XYZ center, double widthFeet, double heightFeet, double rotationRadians, ColumnFootprintSource source)
        {
            // Same convention as a detected rectangle: Width is the longer side, and
            // the rotation is the direction of that side, in (-90°, 90°].
            bool swap = heightFeet > widthFeet;
            double width = swap ? heightFeet : widthFeet;
            double height = swap ? widthFeet : heightFeet;
            double axis = NormalizeAxisAngle(swap ? rotationRadians + Math.PI / 2 : rotationRadians);

            return new ColumnFootprint
            {
                Shape = ColumnFootprintShape.Rectangle,
                Source = source,
                Center = center,
                WidthFeet = width,
                HeightFeet = height,
                RotationRadians = axis,
                Curves = _markerGeometry.BuildRotatedRectangle(center, width, height, axis)
            };
        }

        /// <summary>The element's own plan rotation in the LINKED document, in
        /// radians. FamilyInstance.GetTransform() is used first because, unlike
        /// LocationPoint.Rotation, it also reflects mirroring and flips.</summary>
        public static double GetOwnRotation(Element element)
        {
            if (element is FamilyInstance instance)
            {
                try
                {
                    XYZ basisX = instance.GetTransform().BasisX;
                    if (basisX.GetLength() > 1e-9) return Math.Atan2(basisX.Y, basisX.X);
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException)
                {
                    // fall through to LocationPoint.Rotation
                }
            }

            return element.Location is LocationPoint point ? point.Rotation : 0.0;
        }

        private static double MmToFeet(double mm) => mm / 304.8;

        // ── Anything else ───────────────────────────────────────────────────

        private static ColumnFootprint BuildPolygon(List<Curve> loop)
        {
            XYZ[] starts = loop.Select(c => c.GetEndPoint(0)).ToArray();
            return new ColumnFootprint
            {
                Shape = ColumnFootprintShape.Polygon,
                Center = new XYZ(starts.Average(p => p.X), starts.Average(p => p.Y), starts.Average(p => p.Z)),
                Curves = loop
            };
        }
    }
}
