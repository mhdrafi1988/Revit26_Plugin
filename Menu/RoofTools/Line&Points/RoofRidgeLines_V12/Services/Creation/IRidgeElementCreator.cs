using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Creation
{
    public interface IRidgeElementCreator
    {
        DetailLine CreateBaseLine(RoofRidgeContext context);
        IList<DetailLine> CreatePerpendicularLines(RoofRidgeContext context);
        int AddShapePoints(RoofRidgeContext context, IList<DetailLine> lines);
    }
}
