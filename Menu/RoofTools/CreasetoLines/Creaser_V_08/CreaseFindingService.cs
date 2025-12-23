using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V08.Commands.GeometryHelpers;

namespace Revit26_Plugin.Creaser_V08.Commands.Services
{
    public class CreaseFindingService
    {
        public IDictionary<XYZ, List<XYZ>> BuildCreaseGraph(
            IList<XYZ> corners,
            IList<XYZ> drains)
        {
            return TopFaceCreaseGraphBuilder.BuildGraph(corners, drains);
        }
    }
}
