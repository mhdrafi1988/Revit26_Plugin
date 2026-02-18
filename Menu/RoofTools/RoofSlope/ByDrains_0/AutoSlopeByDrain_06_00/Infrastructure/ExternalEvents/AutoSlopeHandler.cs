using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByDrain_06_00.Core.Engine;
using Revit26_Plugin.AutoSlopeByDrain_06_00.Core.Models;

namespace Revit26_Plugin.AutoSlopeByDrain_06_00.Infrastructure.ExternalEvents
{
    public class AutoSlopeHandler : IExternalEventHandler
    {
        public static AutoSlopePayload Payload;

        public void Execute(UIApplication app)
        {
            if (Payload == null) return;

            using (TransactionGroup tg = new TransactionGroup(app.ActiveUIDocument.Document, "AutoSlope"))
            {
                tg.Start();
                AutoSlopeEngine.Execute(app, Payload);
                tg.Assimilate();
            }
        }

        public string GetName() => "AutoSlope Handler";
    }
}