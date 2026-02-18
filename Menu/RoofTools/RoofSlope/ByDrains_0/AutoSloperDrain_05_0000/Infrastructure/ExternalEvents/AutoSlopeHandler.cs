using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlope.V5_00.Core.Engine;
using Revit26_Plugin.AutoSlope.V5_00.Core.Models;
using Revit26_Plugin.AutoSlope.V5_00.Infrastructure.Helpers;
using System;
using System.Windows;

namespace Revit26_Plugin.AutoSlope.V5_00.Infrastructure.ExternalEvents
{
    public class AutoSlopeHandler : IExternalEventHandler
    {
        private AutoSlopePayload _payload;
        private readonly object _lock = new object();

        public void SetPayload(AutoSlopePayload payload)
        {
            lock (_lock)
            {
                _payload = payload;
            }
        }

        public void Execute(UIApplication app)
        {
            AutoSlopePayload localPayload = null;

            lock (_lock)
            {
                localPayload = _payload;
                _payload = null;
            }

            if (localPayload == null) return;

            Document doc = app.ActiveUIDocument.Document;

            // Use TransactionGroup for overall operation
            using (TransactionGroup tg = new TransactionGroup(doc, "AutoSlope V5"))
            {
                tg.Start();

                try
                {
                    // Verify roof is still valid
                    Element roofElem = doc.GetElement(localPayload.RoofId);
                    if (roofElem == null)
                    {
                        throw new Exception("Roof element no longer exists in document.");
                    }

                    // Process slopes in its own transaction
                    var processor = new SlopeProcessorService();
                    var metrics = processor.ProcessRoofSlopes(localPayload);

                    // Update ViewModel on UI thread
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            localPayload.Vm.VerticesProcessed = metrics.Processed;
                            localPayload.Vm.VerticesSkipped = metrics.Skipped;
                            localPayload.Vm.HighestElevation_mm = metrics.HighestElevation;
                            localPayload.Vm.LongestPath_m = metrics.LongestPath;
                            localPayload.Vm.RunDuration_sec = metrics.DurationSeconds;
                            localPayload.Vm.RunDate = metrics.RunDate;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to update ViewModel: {ex.Message}");
                        }
                    });

                    // If everything succeeded, assimilate the transaction group
                    tg.Assimilate();

                    localPayload.Log(LogColorHelper.Green("✅ AutoSlope completed successfully!"));
                }
                catch (Exception ex)
                {
                    // If anything failed, roll back everything
                    tg.RollBack();
                    localPayload.Log(LogColorHelper.Red($"❌ AutoSlope failed: {ex.Message}"));

                    // Update UI to show failure
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            localPayload.Vm.HasRun = false;
                        }
                        catch { }
                    });
                }
            }
        }

        public string GetName() => "AutoSlope V5 Handler";
    }
}