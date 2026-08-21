using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.RoofLoopAnalyzerPDC.V005.UI.ViewModels;
using System;

namespace Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Infrastructure.ExternalEvents
{
    /// <summary>Work items this tool can ask Revit to perform.</summary>
    public enum RoofLoopAnalyzerPDCRequest
    {
        None,
        AnalyzeRoof,
        ApplyDivisions,
    }

    /// <summary>
    /// Every Revit API call for RoofLoopAnalyzerPDC goes through here. The window is modeless,
    /// so UI-thread code must never touch the Revit API directly — it sets
    /// PendingRequest and calls ExternalEvent.Raise(), and Revit calls Execute()
    /// back on its own thread.
    /// </summary>
    public class RoofLoopAnalyzerPDCEventHandler : IExternalEventHandler
    {
        private readonly RoofLoopAnalyzerPDCViewModel _vm;

        public RoofLoopAnalyzerPDCRequest PendingRequest { get; set; } = RoofLoopAnalyzerPDCRequest.None;

        public RoofLoopAnalyzerPDCEventHandler(RoofLoopAnalyzerPDCViewModel vm) => _vm = vm;

        public void Execute(UIApplication app)
        {
            var request = PendingRequest;
            PendingRequest = RoofLoopAnalyzerPDCRequest.None;

            try
            {
                switch (request)
                {
                    case RoofLoopAnalyzerPDCRequest.AnalyzeRoof:
                        _vm.ExecuteAnalyzeRoofCore();
                        break;
                    case RoofLoopAnalyzerPDCRequest.ApplyDivisions:
                        _vm.ExecuteApplyDivisionsCore();
                        break;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled a pick — silent skip, logged as a warning only.
                _vm.LogFromHandler(LogLevel.Warning, $"{request} cancelled by user.");
            }
            catch (Exception ex)
            {
                // Critical / transaction error — surfaced to the user AND logged.
                _vm.LogFromHandler(LogLevel.Error, $"{request} failed: {ex.Message}");
                TaskDialog.Show("RoofLoopAnalyzerPDC", $"The operation could not be completed.\n\n{ex.Message}");
            }
        }

        public string GetName() => "RoofLoopAnalyzerPDC External Event Handler";
    }
}
