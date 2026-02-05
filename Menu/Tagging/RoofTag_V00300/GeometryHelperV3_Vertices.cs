using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.RoofTagV3
{
    /// <summary>
    /// Vertex extraction logic for RoofTag V3.
    /// Ensures one vertex per unique XY location.
    /// </summary>
    public static partial class GeometryHelperV3
    {
        /// <summary>
        /// Returns one slab shape vertex per unique XY.
        /// If multiple vertices share the same XY,
        /// the highest Z is selected.
        /// </summary>
        public static List<XYZ> GetExactShapeVertices(RoofBase roof)
        {
            if (roof == null)
                throw new ArgumentNullException(nameof(roof));

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null || !editor.IsEnabled)
                return new List<XYZ>();

            const double xyTol = 1e-6;

            return editor.SlabShapeVertices
                .Cast<SlabShapeVertex>()
                .GroupBy(v => new XYKey(v.Position, xyTol))
                .Select(g =>
                    g.OrderByDescending(v => v.Position.Z)
                     .First()
                     .Position)
                .ToList();
        }

        // ------------------------------------------------------------
        // XY grouping key with tolerance
        // ------------------------------------------------------------
        private readonly struct XYKey : IEquatable<XYKey>
        {
            private readonly long _x;
            private readonly long _y;

            public XYKey(XYZ p, double tol)
            {
                _x = (long)Math.Round(p.X / tol);
                _y = (long)Math.Round(p.Y / tol);
            }

            public bool Equals(XYKey other)
                => _x == other._x && _y == other._y;

            public override bool Equals(object obj)
                => obj is XYKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_x.GetHashCode() * 397) ^ _y.GetHashCode();
                }
            }
        }
    }
}
