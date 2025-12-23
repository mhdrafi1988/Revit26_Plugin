using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.Services
{
    public class CreaseToLineService
    {
        public IList<Line> Convert(IList<Line> creaseLines)
        {
            // Direct pass-through, reserved for future expansion
            return new List<Line>(creaseLines);
        }
    }
}
