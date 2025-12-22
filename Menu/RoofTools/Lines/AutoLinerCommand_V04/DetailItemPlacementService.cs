using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutoLiner_V04.Services
{
    public class DetailItemPlacementService
    {
        private readonly UiLogService _log;

        public DetailItemPlacementService(UiLogService log)
        {
            _log = log;
        }

        public void PlacePaths(
            Document doc,
            View view,
            FamilySymbol symbol,
            IList<IList<XYZ>> paths)
        {
            Plane plane = view.SketchPlane.GetPlane();
            int placed = 0;

            foreach (var path in paths)
            {
                for (int i = 0; i < path.Count - 1; i++)
                {
                    XYZ p1 = ProjectToPlane(path[i], plane);
                    XYZ p2 = ProjectToPlane(path[i + 1], plane);

                    if (p1.DistanceTo(p2) < 1e-6)
                        continue;

                    if (System.Math.Abs(p1.Z - p2.Z) > 1e-6)
                        continue;

                    try
                    {
                        doc.Create.NewFamilyInstance(
                            Line.CreateBound(p1, p2),
                            symbol,
                            view);

                        placed++;
                    }
                    catch
                    {
                        _log.Info("Skipped non line-based family");
                    }
                }
            }

            _log.Info($"Detail items placed: {placed}");
        }

        private XYZ ProjectToPlane(XYZ point, Plane plane)
        {
            XYZ v = point - plane.Origin;
            double d = v.DotProduct(plane.Normal);
            return point - d * plane.Normal;
        }
    }
}
