using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;

namespace Revit26_Plugin.V5_00.Infrastructure.Revit
{
    /// <summary>
    /// Creates detail lines from cleaned segments,
    /// only when the active view is a plan view.
    /// </summary>
    public class DetailLineCreatorService
    {
        public void DrawDetailLinesIfPlan(
            UIDocument uiDoc,
            List<(XYZ, XYZ)> segments)
        {
            if (uiDoc == null || segments == null || segments.Count == 0)
                return;

            View view = uiDoc.ActiveView;
            if (!IsPlanView(view))
                return;

            Document doc = uiDoc.Document;

            using (Transaction tx = new Transaction(doc, "AutoSlope Detail Lines"))
            {
                tx.Start();

                foreach (var seg in segments)
                {
                    Line line = Line.CreateBound(seg.Item1, seg.Item2);
                    doc.Create.NewDetailCurve(view, line);
                }

                tx.Commit();
            }
        }

        private bool IsPlanView(View view)
        {
            if (view == null)
                return false;

            switch (view.ViewType)
            {
                case ViewType.FloorPlan:
                case ViewType.EngineeringPlan:
                case ViewType.CeilingPlan:
                    return true;
                default:
                    return false;
            }
        }
    }
}
