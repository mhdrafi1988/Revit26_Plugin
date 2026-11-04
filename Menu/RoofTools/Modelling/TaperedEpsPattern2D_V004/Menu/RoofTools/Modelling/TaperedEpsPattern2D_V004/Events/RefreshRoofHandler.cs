using System;
using Autodesk.Revit.UI;
using Revit26_Plugin.TaperedEpsPattern2D.V004.Revit;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.TaperedEpsPattern2D.V004.Events
{
    /// <summary>Re-reads the picked roof (used by Reset) so edits made while the window is open are picked up.</summary>
    public sealed class RefreshRoofHandler : IExternalEventHandler
    {
        public RoofContext? Current { get; set; }

        public event Action<RoofContext>? Completed;

        /// <summary>Revit entry point — guarded so no exception reaches Revit.</summary>
        public void Execute(UIApplication app)
            => ToolGuard.RunHandler(GetType(), app, ExecuteUnguarded);

        private void ExecuteUnguarded(UIApplication app)
        {
            RoofContext? current = Current;
            if (current == null)
            {
                return;
            }

            RoofContext refreshed;
            try
            {
                refreshed = current.Document.IsValidObject
                    ? RoofContextBuilder.Build(current.Document, current.ViewId, current.RoofId)
                    : current;
            }
            catch (Exception)
            {
                refreshed = current;
            }

            Completed?.Invoke(refreshed);
        }

        public string GetName() => "Tapered EPS 2D Pattern – Refresh roof";
    }
}
