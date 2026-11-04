using System;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004
{
    /// <summary>
    /// Re-scans the view's annotations from the Refresh button (v4.1.0). The scan reads the
    /// Revit API, so it must run inside an external event, not straight from the WPF click.
    /// </summary>
    public class RefreshAnnotationsEventHandler : IExternalEventHandler
    {
        /// <summary>Work to run in the Revit API context (set by the view model).</summary>
        public Action<UIApplication> Work { get; set; }

        /// <inheritdoc />
        public void Execute(UIApplication app)
            => global::Revit26_Plugin.Shared.Services.ToolGuard.RunHandler(GetType(), app, a => Work?.Invoke(a));

        /// <inheritdoc />
        public string GetName() => "Refresh Annotation Overlap Scan";
    }
}
