// ==============================================
// File: WorksetActionHandler.cs
// Layer: Core/Services
// Workset Creator V015.
// Changes vs Workset Manager V014:
//   - Execute runs through ToolGuard.RunHandler, so nothing from this tool
//     reaches Revit's idling loop unhandled (Shared/Services/ToolGuard.cs).
//   - GetName now reports this tool/version (V014 still said
//     "Workset Manager V012").
// ==============================================

using Autodesk.Revit.UI;
using Revit26_Plugin_Revised.Shared.Services;
using System;

namespace Revit26_Plugin_Revised.WorksetCreator.V015.Core.Services
{
    public sealed class WorksetActionHandler : IExternalEventHandler
    {
        private Action<UIApplication> _pending;

        public void Queue(Action<UIApplication> action) => _pending = action;

        public void Execute(UIApplication app)
        {
            var action = _pending;
            _pending = null;
            if (action == null) return;

            ToolGuard.RunHandler(typeof(WorksetActionHandler), app, action);
        }

        public string GetName() => "Workset Creator V015";
    }
}
