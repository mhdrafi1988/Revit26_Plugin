// ==============================================
// File: ConvertExternalEventHandler.cs
// Layer: Helpers
// ==============================================

using Autodesk.Revit.UI;
using System;

namespace Revit26_Plugin.DwgToDetailLines.V001.Helpers
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

        public void Raise(ExternalEvent externalEvent, Action<UIApplication> action)
        {
            _pendingAction = action;
            externalEvent.Raise();
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _pendingAction?.Invoke(app);
            }
            finally
            {
                _pendingAction = null;
            }
        }

        public string GetName() => "DWG to Detail Lines - Convert";
    }
}
