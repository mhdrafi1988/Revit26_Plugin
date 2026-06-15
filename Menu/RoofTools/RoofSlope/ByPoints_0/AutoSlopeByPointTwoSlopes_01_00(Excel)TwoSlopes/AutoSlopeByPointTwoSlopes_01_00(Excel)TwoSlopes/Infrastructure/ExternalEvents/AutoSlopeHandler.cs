using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoSlopeByPointTwoSlopes_01_00.Core.Engine;
using AutoSlopeByPointTwoSlopes_01_00.Core.Models;

namespace AutoSlopeByPointTwoSlopes_01_00.Infrastructure.ExternalEvents
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