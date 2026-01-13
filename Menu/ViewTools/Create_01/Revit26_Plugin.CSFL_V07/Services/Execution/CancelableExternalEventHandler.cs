using Autodesk.Revit.UI;
using System;

namespace Revit26_Plugin.CSFL_V07.Services.Execution
{
    /// <summary>
    /// Executes Revit API logic safely from modeless UI.
    /// </summary>
    public class CancelableExternalEventHandler : IExternalEventHandler
    {
        private readonly Action<UIApplication> _action;

        public CancelableExternalEventHandler(Action<UIApplication> action)
        {
            _action = action;
        }

        public void Execute(UIApplication app)
        {
            _action?.Invoke(app);
        }

        public string GetName()
            => "CSFL Cancelable External Event";
    }
}
