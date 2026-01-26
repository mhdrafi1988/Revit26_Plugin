using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofFromFloor.Services
{
    public static class CurveDumpService
    {
        public static void DumpCurves(
            Document doc,
            View view,
            IEnumerable<Curve> curves,
            string groupName)
        {
            using (Transaction tx = new Transaction(doc, "Dump Debug Curves"))
            {
                tx.Start();

                var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero);
                var sp = SketchPlane.Create(doc, plane);

                var ids = new List<ElementId>();

                foreach (var curve in curves)
                {
                    DetailCurve dc = doc.Create.NewDetailCurve(view, curve);
                    ids.Add(dc.Id);
                }

                if (ids.Count > 0)
                {
                    Group g = doc.Create.NewGroup(ids);
                    g.GroupType.Name = groupName;
                }

                tx.Commit();
            }
        }
    }
}
