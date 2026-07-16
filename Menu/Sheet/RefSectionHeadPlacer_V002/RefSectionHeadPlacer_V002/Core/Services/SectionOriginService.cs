using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.RefSectionHeadPlacer.V002.Infrastructure.Helpers;

namespace Revit26_Plugin.RefSectionHeadPlacer.V002.Core.Services
{
    public class SectionOriginResult
    {
        public XYZ Origin { get; set; }          // HOST coordinates
        public XYZ ViewDirection { get; set; }   // HOST coordinates
        public bool IsValid { get; set; } = true;
        public string SkipReason { get; set; }
    }

    /// <summary>
    /// Computes the reference-section origin per category. ALL outputs are in
    /// HOST coordinates: for linked elements the raw geometry is in link space,
    /// so every point/vector is pushed through <paramref name="toHost"/>
    /// (link.GetTotalTransform()) before returning. For host elements toHost is
    /// the identity transform.
    ///
    /// Rules (confirmed):
    ///   Plumbing Fixtures (drain/wash basin) -> LocationPoint
    ///   Plumbing Equipment (e.g. water heater) -> LocationPoint (same as Plumbing Fixtures)
    ///   Doors                                 -> opening midpoint, cut along wall
    ///   Roofs                                 -> bounding-box centroid (host)
    ///   Walls                                 -> nearest wall–roof corner (host roofs)
    /// </summary>
    public class SectionOriginService
    {
        public SectionOriginResult GetOrigin(
            Element elem, BuiltInCategory bic, Transform toHost, IReadOnlyList<Element> hostRoofs)
        {
            // Route on the STABLE BuiltInCategory, not the localized display name —
            // a name-based switch silently fails on non-English Revit installs.
            switch (bic)
            {
                case BuiltInCategory.OST_Doors:            return DoorOrigin(elem, toHost);
                case BuiltInCategory.OST_PlumbingFixtures: return PointOrigin(elem, toHost);
                case BuiltInCategory.OST_PlumbingEquipment: return PointOrigin(elem, toHost);
                case BuiltInCategory.OST_Roofs:            return RoofCentroid(elem, toHost);
                case BuiltInCategory.OST_Walls:            return WallCorner(elem, toHost, hostRoofs);
                default:
                    return Invalid($"Unsupported category '{bic}'.");
            }
        }

        private static SectionOriginResult PointOrigin(Element elem, Transform toHost)
        {
            if (elem.Location is LocationPoint lp)
            {
                var facing = (elem as FamilyInstance)?.FacingOrientation ?? XYZ.BasisY;
                return Host(toHost, lp.Point, facing);
            }
            return Invalid("Fixture has no LocationPoint.");
        }

        private static SectionOriginResult DoorOrigin(Element elem, Transform toHost)
        {
            if (!(elem is FamilyInstance fi) || !(fi.Location is LocationPoint lp))
                return Invalid("Door has no LocationPoint.");

            // Section should look ALONG the wall run so the cut passes through the
            // door width -> use HandOrientation (runs along wall), fall back to facing.
            XYZ dir = GeometryHelper.SafeNormalize(fi.HandOrientation, fi.FacingOrientation);
            return Host(toHost, lp.Point, dir);
        }

        private static SectionOriginResult RoofCentroid(Element elem, Transform toHost)
        {
            var bbox = elem.get_BoundingBox(null);
            if (bbox == null) return Invalid("Roof has no bounding box.");
            XYZ centroid = (bbox.Min + bbox.Max) * 0.5;
            return Host(toHost, centroid, XYZ.BasisY);
        }

        /// <summary>
        /// Wall (linked) -> nearest corner of a HOST roof's bounding box to either
        /// wall endpoint. Wall endpoints are transformed to host space first so the
        /// distance comparison is apples-to-apples with host roofs.
        /// FLAGGED heuristic — validate against real geometry before production use.
        /// </summary>
        private static SectionOriginResult WallCorner(Element elem, Transform toHost, IReadOnlyList<Element> hostRoofs)
        {
            if (!(elem.Location is LocationCurve lc))
                return Invalid("Wall has no LocationCurve.");

            XYZ p0 = toHost.OfPoint(lc.Curve.GetEndPoint(0));
            XYZ p1 = toHost.OfPoint(lc.Curve.GetEndPoint(1));

            XYZ best = null; double bestDist = double.MaxValue;

            foreach (var roof in hostRoofs)
            {
                var b = roof.get_BoundingBox(null);
                if (b == null) continue;

                var corners = new[]
                {
                    new XYZ(b.Min.X, b.Min.Y, b.Min.Z), new XYZ(b.Min.X, b.Max.Y, b.Min.Z),
                    new XYZ(b.Max.X, b.Min.Y, b.Min.Z), new XYZ(b.Max.X, b.Max.Y, b.Min.Z)
                };
                foreach (var c in corners)
                {
                    double d = System.Math.Min(c.DistanceTo(p0), c.DistanceTo(p1));
                    if (d < bestDist) { bestDist = d; best = c; }
                }
            }

            if (best == null) return Invalid("No host roof found to resolve a wall–roof corner.");

            XYZ wallDir = GeometryHelper.SafeNormalize(p1 - p0, XYZ.BasisX);
            return new SectionOriginResult { Origin = best, ViewDirection = wallDir };
        }

        // Origin/direction already in source space -> push both to host.
        private static SectionOriginResult Host(Transform toHost, XYZ origin, XYZ dir)
            => new SectionOriginResult
            {
                Origin = toHost.OfPoint(origin),
                ViewDirection = GeometryHelper.SafeNormalize(toHost.OfVector(dir), XYZ.BasisY)
            };

        private static SectionOriginResult Invalid(string reason)
            => new SectionOriginResult { IsValid = false, SkipReason = reason };
    }
}
