using Autodesk.Revit.DB;
using System;

namespace Revit22_Plugin.AutoRoofSections.Services
{
    public class SectionDirectionResolver
    {
        private readonly Action<string> _log;

        public SectionDirectionResolver(Action<string> log)
        {
            _log = log;
        }

        public XYZ ResolveDirection(XYZ edgeDir, string mode)
        {
            edgeDir = new XYZ(edgeDir.X, edgeDir.Y, 0).Normalize();

            if (mode == "Force North")
                return new XYZ(0, 1, 0);

            if (mode == "Force East")
                return new XYZ(1, 0, 0);

            // AUTO MODE
            double ax = Math.Abs(edgeDir.X);
            double ay = Math.Abs(edgeDir.Y);

            if (ax > ay)
            {
                _log("Horizontal edge → Facing NORTH");
                return new XYZ(0, 1, 0);
            }

            if (ay > ax)
            {
                _log("Vertical edge → Facing EAST");
                return new XYZ(1, 0, 0);
            }

            // Diagonal → perpendicular
            XYZ perp = new XYZ(-edgeDir.Y, edgeDir.X, 0).Normalize();

            // Make sure Y positive (up)
            if (perp.Y < 0)
                perp = perp.Negate();

            _log("Diagonal edge → Perpendicular direction: " + perp);

            return perp;
        }
    }
}
