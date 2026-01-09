// RoofRidgeWorkflow.cs
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Models;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Creation;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Workflow
{
    public class RoofRidgeWorkflow
    {
        private readonly IRidgeElementCreator _creator;

        public RoofRidgeWorkflow(IRidgeElementCreator creator)
        {
            _creator = creator;
        }

        /// <summary>
        /// Executes logic ONLY. Caller must own an open transaction.
        /// </summary>
        public RoofRidgeResult Execute(RoofRidgeContext context)
        {
            var result = new RoofRidgeResult();

            var baseLine = _creator.CreateBaseLine(context);
            var perpendiculars = _creator.CreatePerpendicularLines(context);
            result.ShapePointsAdded =
                _creator.AddShapePoints(context, perpendiculars);

            result.Success = true;
            return result;
        }
    }
}
