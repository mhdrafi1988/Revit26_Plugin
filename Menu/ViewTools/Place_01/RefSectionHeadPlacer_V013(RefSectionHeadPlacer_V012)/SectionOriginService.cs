using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.RefSectionHeadPlacer.V013.Infrastructure.Helpers;

namespace Revit26_Plugin.RefSectionHeadPlacer.V013.Core.Services
{
    public class SectionOriginResult
    {
        public XYZ Origin { get; set; }          // HOST coordinates
        public XYZ ViewDirection { get; set; }   // HOST coordinates
        public bool IsValid { get; set; } = true;
        public string SkipReason { get; set; }

        /// <summary>V012 (walls only): ordered fallback origins in HOST
        /// coordinates — [midpoint, ¼ point, ¾ point], each already centered.
        /// The engine tries them in order against the clash service; for other
        /// categories this stays null and only Origin is used.</summary>
        public IReadOnlyList<XYZ> CandidateOrigins { get; set; }
    }

    /// <summary>
    /// Computes the reference-section origin per category. ALL outputs are in
    /// HOST coordinates: for linked elements the raw geometry is in link space,
    /// so every point/vector is pushed through <paramref name="toHost"/>
    /// (link.GetTotalTransform()) before returning. For host elements toHost is
    /// the identity transform.
    ///
    /// Rules (confirmed):
    ///   Plumbing Fixtures (drain/wash basin) -> LocationPoint, centered, fixed
    ///                                           screen-space direction (V004)
    ///   Plumbing Equipment (e.g. water heater) -> same as Plumbing Fixtures (V004)
    ///   Doors                                 -> LocationPoint, centered, fixed
    ///                                           screen-space direction (V004),
    ///                                           cut perpendicular to wall
    ///   Roofs                                 -> bounding-box centroid (host)
    ///   Walls (V012, confirmed Rafi)          -> ON the wall's location line,
    ///                                           cut perpendicular to it, one
    ///                                           mark per wall; candidates
    ///                                           mid → ¼ → ¾, engine picks the
    ///                                           first clash-free one
    ///
    /// V012: hostRoofs parameter REMOVED — it existed only for the deleted
    /// wall–roof-corner heuristic (marks were landing where wall met roof edge,
    /// not on the wall itself).
    /// </summary>
    public class SectionOriginService
    {
        public SectionOriginResult GetOrigin(
            Element elem, BuiltInCategory bic, Transform toHost, double tailLengthFeet)
        {
            // Route on the STABLE BuiltInCategory, not the localized display name —
            // a name-based switch silently fails on non-English Revit installs.
            switch (bic)
            {
                case BuiltInCategory.OST_Doors:            return DoorOrigin(elem, toHost, tailLengthFeet);
                case BuiltInCategory.OST_PlumbingFixtures: return PointOrigin(elem, toHost, tailLengthFeet);
                case BuiltInCategory.OST_PlumbingEquipment: return PointOrigin(elem, toHost, tailLengthFeet);
                case BuiltInCategory.OST_Roofs:            return RoofCentroid(elem, toHost);
                case BuiltInCategory.OST_Walls:            return WallOrigin(elem, toHost, tailLengthFeet);
                default:
                    return Invalid($"Unsupported category '{bic}'.");
            }
        }

        /// <summary>
        /// V004 CHANGE (confirmed with Rafi): applies to ALL point-location-based
        /// items — Plumbing Fixtures AND Plumbing Equipment. Direction is now the
        /// same FIXED absolute screen-space convention as doors (see
        /// ResolveFixedScreenSpaceDirection), not the element's raw
        /// FacingOrientation sign. Origin is centered — the element's LocationPoint
        /// sits at the MIDDLE of the head→tail marker line, same centering
        /// approach as DoorOrigin.
        /// </summary>
        private static SectionOriginResult PointOrigin(Element elem, Transform toHost, double tailLengthFeet)
        {
            if (!(elem.Location is LocationPoint lp))
                return Invalid("Fixture has no LocationPoint.");

            var facing = (elem as FamilyInstance)?.FacingOrientation ?? XYZ.BasisY;
            XYZ pFinal = ResolveFixedScreenSpaceDirection(facing);

            XYZ centeredOrigin = lp.Point - pFinal * (tailLengthFeet / 2.0);
            return Host(toHost, centeredOrigin, pFinal);
        }

        /// <summary>
        /// Shared fixed absolute screen-space direction rule (Rafi-confirmed),
        /// used by both DoorOrigin and PointOrigin. Only the AXIS of the given
        /// facing vector is used; the sign is forced so every element reads the
        /// same way in plan regardless of which way it individually faces:
        ///   - Perpendicular closer to N-S: force Y negative (head=top, tail=bottom).
        ///   - Perpendicular closer to E-W: force X positive (head=west, tail=east,
        ///     view looks east/right).
        /// </summary>
        private static XYZ ResolveFixedScreenSpaceDirection(XYZ facing)
        {
            XYZ f = GeometryHelper.SafeNormalize(facing, XYZ.BasisX);

            XYZ pFinal;
            if (System.Math.Abs(f.Y) >= System.Math.Abs(f.X))
                pFinal = new XYZ(f.X, -System.Math.Abs(f.Y), 0);
            else
                pFinal = new XYZ(System.Math.Abs(f.X), f.Y, 0);

            return GeometryHelper.SafeNormalize(pFinal, XYZ.BasisX);
        }

        /// <summary>
        /// V004 CHANGE (confirmed with Rafi): marker now cuts PERPENDICULAR to the
        /// wall — passes through the door opening, cut looks at the wall face —
        /// instead of V003's along-the-wall (HandOrientation) cut.
        ///
        /// Direction uses the shared ResolveFixedScreenSpaceDirection rule — see
        /// that method for the fixed absolute screen-space convention.
        ///
        /// Origin returned is PRE-OFFSET by half the tail length (toward the head
        /// side) so the door is centered between head and tail once
        /// SectionPlacementService applies tail = Origin + ViewDirection * tailLengthFeet.
        /// This is why GetOrigin/DoorOrigin need tailLengthFeet — WallOrigin (V012)
        /// centers the same way; RoofCentroid alone anchors at the element itself.
        /// </summary>
        private static SectionOriginResult DoorOrigin(Element elem, Transform toHost, double tailLengthFeet)
        {
            if (!(elem is FamilyInstance fi) || !(fi.Location is LocationPoint lp))
                return Invalid("Door has no LocationPoint.");

            XYZ pFinal = ResolveFixedScreenSpaceDirection(fi.FacingOrientation);

            // Centered head: shift back half the tail length from the door point,
            // in SOURCE (link/host) space — Host() below pushes both to host space.
            XYZ centeredOrigin = lp.Point - pFinal * (tailLengthFeet / 2.0);

            return Host(toHost, centeredOrigin, pFinal);
        }

        private static SectionOriginResult RoofCentroid(Element elem, Transform toHost)
        {
            var bbox = elem.get_BoundingBox(null);
            if (bbox == null) return Invalid("Roof has no bounding box.");
            XYZ centroid = (bbox.Min + bbox.Max) * 0.5;
            return Host(toHost, centroid, XYZ.BasisY);
        }

        /// <summary>
        /// V012 REWRITE (confirmed, Rafi). Replaces the V004 wall–roof-corner
        /// heuristic entirely — that landed marks where the wall met a roof edge
        /// instead of on the wall itself, and its cut ran ALONG the wall.
        ///
        /// New rule — one mark per wall, ON the wall:
        ///   - Anchor points on the wall's location line at normalized parameters
        ///     0.5 (mid), 0.25, 0.75 — evaluated via Curve.Evaluate so arc walls
        ///     get true curve points, not chord interpolations.
        ///   - Cut PERPENDICULAR to the wall line (tangent at the midpoint for
        ///     arcs), with the sign forced through the same fixed screen-space
        ///     convention as doors/fixtures (ResolveFixedScreenSpaceDirection).
        ///   - Each candidate is CENTERED like DoorOrigin: shifted back half the
        ///     tail length so the wall line sits mid-way between head and tail.
        ///   - All three candidates go out in CandidateOrigins (host coords,
        ///     mid first); the engine takes the first clash-free one, then falls
        ///     back to the radial nudge, then skips with a Warning.
        /// </summary>
        private static SectionOriginResult WallOrigin(Element elem, Transform toHost, double tailLengthFeet)
        {
            if (!(elem.Location is LocationCurve lc) || lc.Curve == null)
                return Invalid("Wall has no LocationCurve.");

            Curve curve = lc.Curve;

            // Wall tangent at mid-parameter (source space). For lines this equals
            // end-end direction; for arc walls it's the true mid tangent.
            XYZ mid   = curve.Evaluate(0.5, normalized: true);
            XYZ after = curve.Evaluate(0.55, normalized: true);
            XYZ tangent = GeometryHelper.SafeNormalize(after - mid, XYZ.BasisX);

            // Perpendicular in plan, then the shared fixed screen-space sign rule
            // so every wall mark reads the same way regardless of wall direction.
            XYZ perp = GeometryHelper.SafeNormalize(new XYZ(-tangent.Y, tangent.X, 0), XYZ.BasisY);
            XYZ dir = ResolveFixedScreenSpaceDirection(perp);
            XYZ hostDir = GeometryHelper.SafeNormalize(toHost.OfVector(dir), XYZ.BasisY);

            // Candidates mid → ¼ → ¾, each centered (wall line mid-way between
            // head and tail, same approach as DoorOrigin), pushed to host space.
            var candidates = new List<XYZ>(3);
            foreach (double p in new[] { 0.5, 0.25, 0.75 })
            {
                XYZ onWall = curve.Evaluate(p, normalized: true);
                XYZ centered = onWall - dir * (tailLengthFeet / 2.0);
                candidates.Add(toHost.OfPoint(centered));
            }

            return new SectionOriginResult
            {
                Origin = candidates[0],
                ViewDirection = hostDir,
                CandidateOrigins = candidates
            };
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
