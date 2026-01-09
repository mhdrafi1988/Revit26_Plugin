using Autodesk.Revit.DB;
using System;

namespace Revit22_Plugin.RoofTagV4.Helpers
{
    /// <summary>
    /// Creates small reference planes at tag points for plan-view SpotElevation tagging.
    /// These planes remain visible (Option 3: debug mode).
    /// </summary>
    public static class ReferencePlaneHelperV4
    {
        /// <summary>
        /// Creates a tiny reference plane at the given XY location.
        /// Returns a usable Reference for SpotElevation placement.
        /// </summary>
        public static Reference CreateHelperPlane(Document doc, XYZ pt)
        {
            // Reference plane dimensions (small but visible)
            double half = 0.5; // ft (~150mm)

            XYZ p1 = new XYZ(pt.X - half, pt.Y, pt.Z);
            XYZ p2 = new XYZ(pt.X + half, pt.Y, pt.Z);
            XYZ p3 = new XYZ(pt.X, pt.Y + half, pt.Z);

            string name = $"RoofTagV4_Helper_{Guid.NewGuid():N}";

            ReferencePlane plane = doc.Create.NewReferencePlane(
                p1, p2, p3, doc.ActiveView);

            plane.Name = name;

            // Return reference to plane's geometry
            return plane.GetReference();
        }

        /// <summary>
        /// Creates a helper plane AND returns a reference that is guaranteed stable.
        /// </summary>
        public static Reference GetPlaneReference(Document doc, XYZ pt)
        {
            ReferencePlane rp = CreatePlane(doc, pt);
            return rp?.GetReference();
        }

        private static ReferencePlane CreatePlane(Document doc, XYZ pt)
        {
            double half = 0.5;

            XYZ p1 = new XYZ(pt.X - half, pt.Y, pt.Z);
            XYZ p2 = new XYZ(pt.X + half, pt.Y, pt.Z);
            XYZ p3 = new XYZ(pt.X, pt.Y + half, pt.Z);

            try
            {
                var rp = doc.Create.NewReferencePlane(
                    p1, p2, p3, doc.ActiveView);

                rp.Name = $"RoofTagV4_Helper_{Guid.NewGuid():N}";
                return rp;
            }
            catch
            {
                return null;
            }
        }
    }
}
