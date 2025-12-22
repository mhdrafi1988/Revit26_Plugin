using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoLiner_V02.Services;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoLiner_V02.ExternalEvents
{
    public class AutoLinerExternalEventHandler : IExternalEventHandler
    {
        public Document Document { get; set; }
        public View ActiveView { get; set; }
        public Element Roof { get; set; }
        public FamilySymbol DetailSymbol { get; set; }
        public Action<string> Log { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                using Transaction t =
                    new Transaction(Document, "AutoLiner – Roof Drain Lines");

                t.Start();
                Log?.Invoke("Transaction started");

                List<XYZ> corners =
                    RoofGeometryService.GetCornerPoints(Roof);

                List<XYZ> drains =
                    RoofGeometryService.GetDrainPoints(Roof);

                if (corners.Count == 0 || drains.Count == 0)
                {
                    Log?.Invoke("❌ Invalid roof geometry");
                    t.RollBack();
                    return;
                }

                DebugGeometryService.DrawDebugLines(
                    Document,
                    ActiveView,
                    corners,
                    drains,
                    Log);

                DetailItemCreationService.CreateCornerToDrainLines(
                    Document,
                    ActiveView,
                    DetailSymbol,
                    corners,
                    drains,
                    Log);

                t.Commit();
                Log?.Invoke("Transaction committed");
            }
            catch (Exception ex)
            {
                Log?.Invoke("❌ ERROR");
                Log?.Invoke(ex.Message);
            }
        }

        public string GetName()
        {
            return "AutoLiner External Event";
        }
    }
}
