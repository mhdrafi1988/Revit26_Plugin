using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
//using Revit22_Plugin.AutoSlopeV3.Engine;
using Revit26_Plugin.AutoSlopeByPoint_30_07.Engine;
using Revit26_Plugin.AutoSlopeByPoint_30_07.Models;

namespace Revit26_Plugin.AutoSlopeByPoint_30_07.ExternalEvents
{
    public class AutoSlopeHandler : IExternalEventHandler
    {
        public static AutoSlopePayload Payload;

        public void Execute(UIApplication app)
        {
            if (Payload == null) return;

            using (TransactionGroup tg =
                   new TransactionGroup(app.ActiveUIDocument.Document, "AutoSlope"))
            {
                tg.Start();
                AutoSlopeEngine.Execute(app, Payload);
                tg.Assimilate();
            }
        }

        public string GetName() => "AutoSlope Handler";
    }
}
