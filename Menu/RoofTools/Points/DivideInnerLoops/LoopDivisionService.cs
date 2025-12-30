using Autodesk.Revit.DB;
using Revit22_Plugin.PDCV1.Models;
using System.Collections.Generic;


namespace Revit22_Plugin.PDCV1.Services
{
    public class c
    {
        public void AddDivisionPoints(Document doc, RoofBase roof, IEnumerable<RoofLoopModel> loops)
        {
            using (Transaction tx = new Transaction(doc, "Add Division Points"))
            {
                tx.Start();
                var editor = roof.GetSlabShapeEditor();


                foreach (var loop in loops)
                {
                    if (!loop.IsCircular || loop.RecommendedPoints < 1) continue;


                    foreach (Curve c in loop.Geometry)
                    {
                        for (int i = 0; i < loop.RecommendedPoints; i++)
                        {
                            double param = (double)i / loop.RecommendedPoints;
                            XYZ pt = c.Evaluate(param, true);
                            editor.AddPoint(pt); // Fixed: Use AddPoint instead of DrawPoint
                        }
                    }
                }


                tx.Commit();
            }
        }
    }
}