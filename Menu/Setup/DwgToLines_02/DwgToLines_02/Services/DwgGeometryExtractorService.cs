using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Services
{
    /// <summary>
    /// Extracts curve geometry from a DWG ImportInstance.
    /// </summary>
    public static class DwgGeometryExtractorService
    {
        public static IList<Curve> ExtractCurves(ImportInstance dwg)
        {
            var curves = new List<Curve>();

            Options opt = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geo = dwg.get_Geometry(opt);
            if (geo == null) return curves;

            foreach (GeometryObject obj in geo)
            {
                if (obj is GeometryInstance gi)
                {
                    foreach (GeometryObject instObj in gi.GetInstanceGeometry())
                    {
                        if (instObj is Curve curve)
                        {
                            curves.Add(curve);
                        }
                    }
                }
            }

            return curves;
        }
    }
}
