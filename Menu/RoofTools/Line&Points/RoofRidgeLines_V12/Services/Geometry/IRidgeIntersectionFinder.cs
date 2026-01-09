using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Geometry
{
    public interface IRidgeIntersectionFinder
    {
        IList<XYZ> FindIntersections(RoofBase roof, Line ray);
    }
}
