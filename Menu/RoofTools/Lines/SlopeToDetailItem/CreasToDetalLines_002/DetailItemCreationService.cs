using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V002.Models;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class DetailLineCreationService
    {
        public IList<DrainPathSegment> CreateDetailLines(
            Document doc,
            ViewPlan planView,
            IEnumerable<PathResult> paths)
        {
            var segments = new List<DrainPathSegment>();

            foreach (PathResult path in paths)
            {
                for (int i = 0; i < path.OrderedNodes.Count - 1; i++)
                {
                    XYZ p1 = path.OrderedNodes[i].Point;
                    XYZ p2 = path.OrderedNodes[i + 1].Point;

                    XYZ s = new XYZ(p1.X, p1.Y, planView.GenLevel.Elevation);
                    XYZ e = new XYZ(p2.X, p2.Y, planView.GenLevel.Elevation);

                    Line line = Line.CreateBound(s, e);
                    DetailCurve dc = doc.Create.NewDetailCurve(planView, line);

                    segments.Add(new DrainPathSegment
                    {
                        Start2D = s,
                        End2D = e,
                        DetailCurve = dc
                    });
                }
            }

            return segments;
        }
    }
}
