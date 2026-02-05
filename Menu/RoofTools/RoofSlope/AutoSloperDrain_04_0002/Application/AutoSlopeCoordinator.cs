using Revit22_Plugin.V4_02.Application.Contexts;
using Revit22_Plugin.V4_02.Domain.Models;
using Revit22_Plugin.V4_02.Domain.Processors;

namespace Revit22_Plugin.V4_02.Application.Coordinators
{
    public class AutoSlopeCoordinator
    {
        private readonly RoofSlopeProcessor _processor = new();

        public SlopeResult Execute(AutoSlopeContext context)
        {
            context.Logger.Info("AutoSlope started");
            return _processor.Process(context);
        }
    }
}
