using Autodesk.Revit.UI;
using Revit26_Plugin.V5_00.Application.Contexts;
using Revit26_Plugin.V5_00.Domain.Models;
using Revit26_Plugin.V5_00.Domain.Processors;

namespace Revit26_Plugin.V5_00.Application.Coordinators
{
    public class AutoSlopeCoordinatorV2 // Renamed class
    {
        private readonly RoofSlopeProcessor _roofSlopeProcessor = new();

        public SlopeResult Execute(AutoSlopeContext context, UIDocument uiDoc)
        {
            context.Logger.Info("AutoSlope started");
            var result = _roofSlopeProcessor.Process(context, uiDoc);
            context.Logger.Info("AutoSlope finished");
            return result;
        }
    }
}
