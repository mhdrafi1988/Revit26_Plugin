using Autodesk.Revit.UI;
using System;

namespace Revit26_Plugin.WorksetManager.V013.Core.Services
{
    public sealed class WorksetActionHandler : IExternalEventHandler
    {
        private Action<UIApplication> _pending;

        public void Queue(Action<UIApplication> action) => _pending = action;

        public void Execute(UIApplication app)
            => global::Revit26_Plugin.Shared.Services.ToolGuard.RunHandler(GetType(), app, ExecuteUnguarded);

        private void ExecuteUnguarded(UIApplication app)
        {
            var action = _pending;
            _pending = null;
            action?.Invoke(app);
        }

        public string GetName() => "Workset Manager V012";
    }
}
