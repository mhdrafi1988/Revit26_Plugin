// ==============================================
// File: ConvertExternalEventHandler.cs
// Layer: Infrastructure/ExternalEvents
// ==============================================

using Autodesk.Revit.UI;
using System;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.DwgToDetailLines.V014.Infrastructure.ExternalEvents
{
    /// <summary>
    /// Single orchestrator handler for this tool's one Revit-side action (Convert).
    /// Required because the window is modeless: any Revit API call triggered by a
    /// button click (outside the original IExternalCommand.Execute call) must be
    /// raised through an ExternalEvent, or Revit throws an invalid-API-context error.
    /// </summary>
    public class ConvertExternalEventHandler : IExternalEventHandler
    {
        private Action<UIApplication> _pendingAction;

        /// <summary>Queues <paramref name="action"/> and raises <paramref name="externalEvent"/>.</summary>
        public void Raise(ExternalEvent externalEvent, Action<UIApplication> action)
        {
            _pendingAction = action;
            externalEvent.Raise();
        }

        /// <summary>Runs the pending action; guarded by <see cref="ToolGuard"/>.</summary>
        public void Execute(UIApplication app)
            => ToolGuard.RunHandler(GetType(), app, a =>
            {
                try
                {
                    _pendingAction?.Invoke(a);
                }
                finally
                {
                    _pendingAction = null;
                }
            });

        /// <summary>Handler name shown by Revit.</summary>
        public string GetName() => "DWG to Detail Lines - Convert";
    }
}
