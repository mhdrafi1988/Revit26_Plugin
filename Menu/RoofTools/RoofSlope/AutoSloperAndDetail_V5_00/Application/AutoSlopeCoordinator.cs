using Revit26_Plugin.V5_00.Application.Contexts;
using Revit26_Plugin.V5_00.Domain.Models;
using Revit26_Plugin.V5_00.Domain.Processors;

namespace Revit26_Plugin.V5_00.Application.Coordinators
{
    public class AutoSlopeCoordinator
    {
        private readonly RoofSlopeProcessor _processor = new();

        public SlopeResult Execute(AutoSlopeContext context)
        {
            context.Logger.Info("AutoSlope started");
            var result = _processor.Process(context);
            context.Logger.Info("AutoSlope finished");
            return result;
        }
    }
}
