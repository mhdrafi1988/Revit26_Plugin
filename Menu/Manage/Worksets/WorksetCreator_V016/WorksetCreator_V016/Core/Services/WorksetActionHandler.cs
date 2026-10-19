// ==============================================
// File: WorksetActionHandler.cs
// Layer: Core/Services
// Workset Creator V016 — carried from V015, version string bumped.
//   - Execute runs through ToolGuard.RunHandler, so nothing from this tool
//     reaches Revit's idling loop unhandled (Shared/Services/ToolGuard.cs).
// ==============================================

using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Services;
using System;

namespace Revit26_Plugin.WorksetCreator.V016.Core.Services
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

        public string GetName() => "Workset Creator V016";
    }
}
