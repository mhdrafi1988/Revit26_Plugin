using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class RoofSolidExtractionService
    {
        public Solid ExtractSolid(Element roof)
        {
            if (roof == null) throw new ArgumentNullException(nameof(roof));

            Options options = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geom = roof.get_Geometry(options);

            foreach (GeometryObject obj in geom)
            {
                if (obj is Solid solid && solid.Volume > 0)
                    return solid;
            }

            throw new InvalidOperationException(
                "No valid solid found in roof geometry.");
        }
    }
}
