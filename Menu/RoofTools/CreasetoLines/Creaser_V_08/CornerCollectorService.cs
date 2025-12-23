using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V08.Commands.GeometryHelpers;

namespace Revit26_Plugin.Creaser_V08.Commands.Services
{
    public class CornerCollectorService
    {
        public IList<XYZ> Collect(Element roof)
        {
            return CornerPointExtractor.GetTopFaceCorners(roof);
        }
    }
}
