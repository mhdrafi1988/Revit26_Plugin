using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004
{
    /// <summary>
    /// Step 8: zoom-on-click. The DataGrid click happens on the WPF side, which must not
    /// touch the Revit API directly - so the click sets <see cref="ElementIdsToZoom"/> and
    /// calls Raise(). Revit then calls Execute() in a valid API context.
    /// Since v4.1.0 it zooms to and selects both elements of a pair, and reports
    /// deleted elements through <see cref="ReportMessage"/>. Since v4.2.0 it opens the
    /// pair's view first when another view is active (batch results span many views).
    /// </summary>
    public class ZoomToElementEventHandler : IExternalEventHandler
    {
        /// <summary>Elements to zoom to and select (usually the two elements of a pair).</summary>
        public IList<ElementId> ElementIdsToZoom { get; set; }

        /// <summary>The view the elements were found in; it is opened first if another view is active.</summary>
        public ElementId TargetViewId { get; set; }

        /// <summary>Called with a user-facing message when the zoom cannot be done; null message clears it.</summary>
        public Action<string> ReportMessage { get; set; }

        /// <inheritdoc />
        public void Execute(UIApplication app)
            => global::Revit26_Plugin.Shared.Services.ToolGuard.RunHandler(GetType(), app, ExecuteUnguarded);

        private void ExecuteUnguarded(UIApplication app)
        {
            var ids = ElementIdsToZoom;
            if (ids == null || ids.Count == 0)
                return;

            UIDocument uiDoc = app.ActiveUIDocument;
            if (uiDoc == null)
            {
                ReportMessage?.Invoke("No active document.");
                return;
            }

            Document doc = uiDoc.Document;
            View view = uiDoc.ActiveView;
            if (TargetViewId != null && TargetViewId != ElementId.InvalidElementId && view.Id != TargetViewId)
            {
                if (doc.GetElement(TargetViewId) is not View target || !target.IsValidObject)
                {
                    ReportMessage?.Invoke("The view these elements were found in no longer exists. Click Refresh.");
                    return;
                }

                try
                {
                    uiDoc.ActiveView = target;
                    view = uiDoc.ActiveView;
                }
                catch (Exception ex)
                {
                    ReportMessage?.Invoke($"Could not open view \"{target.Name}\": {ex.Message}");
                    return;
                }

                if (view.Id != TargetViewId)
                {
                    uiDoc.RequestViewChange(target);
                    ReportMessage?.Invoke($"Opening view \"{target.Name}\" - click the ID again to zoom.");
                    return;
                }
            }

            var found = new List<ElementId>();
            var missing = new List<long>();
            XYZ min = null, max = null;

            foreach (ElementId id in ids)
            {
                Element elem = doc.GetElement(id);
                BoundingBoxXYZ bbox = elem?.get_BoundingBox(view);
                if (bbox == null)
                {
                    missing.Add(id.Value);
                    continue;
                }

                found.Add(id);
                min = min == null ? bbox.Min : new XYZ(Math.Min(min.X, bbox.Min.X), Math.Min(min.Y, bbox.Min.Y), Math.Min(min.Z, bbox.Min.Z));
                max = max == null ? bbox.Max : new XYZ(Math.Max(max.X, bbox.Max.X), Math.Max(max.Y, bbox.Max.Y), Math.Max(max.Z, bbox.Max.Z));
            }

            if (found.Count == 0)
            {
                ReportMessage?.Invoke($"Element(s) {string.Join(", ", missing)} no longer exist or are hidden in this view. Click Refresh.");
                return;
            }

            // 10% padding margin around the elements
            XYZ padding = (max - min) * 0.10;
            UIView uiView = uiDoc.GetOpenUIViews().FirstOrDefault(v => v.ViewId == view.Id);
            uiView?.ZoomAndCenterRectangle(min - padding, max + padding);

            uiDoc.Selection.SetElementIds(found);

            ReportMessage?.Invoke(missing.Count == 0
                ? null
                : $"Element(s) {string.Join(", ", missing)} no longer exist or are hidden in this view. Click Refresh.");
        }

        /// <inheritdoc />
        public string GetName() => "Zoom To Annotation Element";
    }
}
