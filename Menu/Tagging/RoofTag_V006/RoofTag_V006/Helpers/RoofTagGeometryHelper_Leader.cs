using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofTag_V006.Helpers
{
    internal static partial class RoofTagGeometryHelper
    {
        internal enum PlacementMode { Inward, Outward }

        // ================================================================
        // TWO-STEP LEADER PLACEMENT
        // Step 1 — diagonal move from anchor  → BEND point
        // Step 2 — orthogonal move from bend  → END  point
        // All calculations in view space, then transformed to model space.
        // ================================================================
        public static (XYZ Bend, XYZ End) ComputeTwoStepLeaderPlacement(
            View          view,
            Element       element,
            XYZ           anchorPoint,
            double        bendOffsetFt,
            double        endOffsetFt,
            PlacementMode placementMode)
        {
            Transform viewInverse = view.CropBox.Transform.Inverse;

            // Element bounding box in view space
            BoundingBoxXYZ bbox = GetElementViewBoundingBox(element, view);
            if (bbox == null)
                return (anchorPoint, anchorPoint);

            XYZ center     = (bbox.Min + bbox.Max) * 0.5;
            XYZ anchorView = viewInverse.OfPoint(anchorPoint);

            // Determine quadrant of anchor relative to element center
            XYZ right = view.RightDirection.Normalize();
            XYZ up    = view.UpDirection.Normalize();
            XYZ delta = anchorView - center;

            bool isTop   = delta.DotProduct(up)    >= 0;
            bool isRight = delta.DotProduct(right)  >= 0;

            // Diagonal direction for BEND (view space unit vectors)
            XYZ diagView = CalculateDiagonalDirection(isTop, isRight, placementMode);
            XYZ diagModel = viewInverse.Inverse.OfVector(diagView).Normalize();
            XYZ bendPoint = anchorPoint + diagModel * bendOffsetFt;

            // Orthogonal direction for END (view space)
            XYZ orthoView  = CalculateOrthogonalDirection(isTop, isRight, placementMode);
            XYZ orthoModel = viewInverse.Inverse.OfVector(orthoView).Normalize();
            XYZ endPoint   = bendPoint + orthoModel * endOffsetFt;

            return (bendPoint, endPoint);
        }

        // ── Diagonal direction table ─────────────────────────────────────
        // OUTWARD: move away from center  |  INWARD: move toward center
        private static XYZ CalculateDiagonalDirection(
            bool isTop, bool isRight, PlacementMode mode)
        {
            XYZ r = new XYZ(1, 0, 0);   // view-space X
            XYZ u = new XYZ(0, 1, 0);   // view-space Y

            if (mode == PlacementMode.Outward)
            {
                if ( isTop &&  isRight) return  r +  u;  // ↗
                if ( isTop && !isRight) return -r +  u;  // ↖
                if (!isTop &&  isRight) return  r + -u;  // ↘
                                        return -r + -u;  // ↙
            }
            else // Inward — opposite quadrant
            {
                if ( isTop &&  isRight) return -r + -u;  // ↙
                if ( isTop && !isRight) return  r + -u;  // ↘
                if (!isTop &&  isRight) return -r +  u;  // ↖
                                        return  r +  u;  // ↗
            }
        }

        // ── Orthogonal direction (horizontal tail of leader) ─────────────
        private static XYZ CalculateOrthogonalDirection(
            bool isTop, bool isRight, PlacementMode mode)
        {
            XYZ r = new XYZ(1, 0, 0);

            // Outward → same horizontal side as anchor
            // Inward  → opposite horizontal side
            bool goRight = mode == PlacementMode.Outward ? isRight : !isRight;
            return goRight ? r : -r;
        }

        // ── Element bounding box in view coordinates ─────────────────────
        private static BoundingBoxXYZ GetElementViewBoundingBox(Element element, View view)
        {
            Options opts = new Options
            {
                View              = view,
                ComputeReferences = true
            };

            GeometryElement geo = element.get_Geometry(opts);
            if (geo == null) return null;

            BoundingBoxXYZ bbox = geo.GetBoundingBox();
            if (bbox == null) return null;

            Transform inv = view.CropBox.Transform.Inverse;
            return new BoundingBoxXYZ
            {
                Min = inv.OfPoint(bbox.Min),
                Max = inv.OfPoint(bbox.Max)
            };
        }
    }
}
