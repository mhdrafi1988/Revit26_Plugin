using Autodesk.Revit.UI;
using System;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.WorksetManager.V012.Core.Services
{
    public sealed class WorksetActionHandler : IExternalEventHandler
    {
        private Action<UIApplication> _pending;

        public void Queue(Action<UIApplication> action) => _pending = action;

        public void Execute(UIApplication app)
            => ToolGuard.RunHandler(GetType(), app, ExecuteUnguarded);

        private void ExecuteUnguarded(UIApplication app)
        {
            var action = _pending;
            _pending = null;
            action?.Invoke(app);
        }

        public string GetName() => "Workset Manager V012";
    }
}
