using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V08.Commands.GeometryHelpers;

namespace Revit26_Plugin.Creaser_V08.Commands.Services
{
    public class DrainDetectionService
    {
        public IList<XYZ> Detect(
            IList<XYZ> shapePoints,
            double toleranceInternal,
            double clusterRadiusInternal,
            out double lowestZ,
            out int rawCandidates,
            out int clusterCount)
        {
            return DrainPointAnalyzer.DetectDrains(
                shapePoints,
                toleranceInternal,
                clusterRadiusInternal,
                out lowestZ,
                out rawCandidates,
                out clusterCount);
        }
    }
}
