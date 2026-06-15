using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Creaser_adv_V001.Models;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    /// <summary>
    /// Creates 2D detail lines from 3D drainage paths.
    /// Z is used ONLY for direction logic.
    /// Geometry is strictly view-based (DetailCurves).
    /// </summary>
    public class DetailLineCreationService
    {
        /// <summary>
        /// Creates detail lines for all valid path results.
        /// Must be called inside a Revit transaction.
        /// </summary>
        public IList<DrainPathSegment> CreateDetailLines(
            Document doc,
            ViewPlan planView,
            IEnumerable<PathResult> pathResults)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (planView == null) throw new ArgumentNullException(nameof(planView));

            var createdSegments = new List<DrainPathSegment>();

            foreach (PathResult path in pathResults)
            {
                if (!path.PathFound || path.OrderedNodes.Count < 2)
                    continue;

                for (int i = 0; i < path.OrderedNodes.Count - 1; i++)
                {
                    XYZ p1 = path.OrderedNodes[i].Point;
                    XYZ p2 = path.OrderedNodes[i + 1].Point;

                    // ---------------------------------
                    // Enforce high Z -> low Z direction
                    // ---------------------------------
                    XYZ high = p1.Z >= p2.Z ? p1 : p2;
                    XYZ low = p1.Z >= p2.Z ? p2 : p1;

                    // ---------------------------------
                    // Project to 2D (plan view)
                    // ---------------------------------
                    XYZ start2D = new XYZ(high.X, high.Y, planView.GenLevel.Elevation);
                    XYZ end2D = new XYZ(low.X, low.Y, planView.GenLevel.Elevation);

                    Line detailLine = Line.CreateBound(start2D, end2D);

                    DetailCurve dc = doc.Create.NewDetailCurve(planView, detailLine);

                    createdSegments.Add(new DrainPathSegment
                    {
                        Start3D = high,
                        End3D = low,
                        Start2D = start2D,
                        End2D = end2D,
                        DetailCurve = dc
                    });
                }
            }

            return createdSegments;
        }
    }
}
