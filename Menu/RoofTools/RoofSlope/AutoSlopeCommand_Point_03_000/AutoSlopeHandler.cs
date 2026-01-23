using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using Revit22_Plugin.AutoSlopeV3.Engine;
using Revit22_Plugin.AutoSlopeV3.Helpers;

namespace Revit22_Plugin.AutoSlopeV3.Handlers
{
    public class AutoSlopeHandler : IExternalEventHandler
    {
        public static AutoSlopePayload Payload { get; set; }

        public void Execute(UIApplication app)
        {
            if (Payload == null)
                return;

            Document doc = app.ActiveUIDocument.Document;

            try
            {
                using (TransactionGroup tg = new TransactionGroup(doc, "AutoSlope v11"))
                {
                    tg.Start();
                    AutoSlopeEngine.Execute(app, Payload);
                    tg.Assimilate();
                }
            }
            catch (Exception ex)
            {
                Payload.Log(LogColorHelper.Red("ENGINE ERROR: " + ex.Message));
            }
        }

        public string GetName() => "AutoSlope Handler";
    }
}
