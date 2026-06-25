using System;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.RoofDetailLineIntersect.V004
{
    /// <summary>
    /// Bridges the WPF UI thread to the Revit API thread.
    /// Created inside IExternalCommand.Execute() — the only valid context for ExternalEvent.Create().
    /// </summary>
    public sealed class PlacePointsEventHandler : IExternalEventHandler
    {
        private readonly RoofDetailLineIntersectViewModel _vm;

        public PlacePointsEventHandler(RoofDetailLineIntersectViewModel vm) => _vm = vm;

        public string GetName() => "RoofDetailLineIntersect V004 — Place Shape Points";

        public void Execute(UIApplication app)
        {
            try   { _vm.ExecutePlacePoints(); }
            catch (Exception ex)
            { _vm.AddLog(Revit26_Plugin.Shared.Models.LogLevel.Error, $"Event handler error: {ex.Message}"); }
            finally
            { _vm.IsBusy = false; }
        }
    }
}
