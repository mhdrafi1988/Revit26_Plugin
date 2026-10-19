using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DwgStatistics.V005.Infrastructure.RevitApi
{
    /// <summary>One view to open, and the CAD instances to select / zoom to once it is open.</summary>
    public sealed class DwgOpenRequest
    {
        /// <summary>Creates a request.</summary>
        public DwgOpenRequest(long viewId, string label, IReadOnlyList<long> zoomInstanceIds)
        {
            ViewId = viewId;
            Label = label;
            ZoomInstanceIds = zoomInstanceIds ?? Array.Empty<long>();
        }

        /// <summary>View (or sheet) to open.</summary>
        public long ViewId { get; }

        /// <summary>Name for the log.</summary>
        public string Label { get; }

        /// <summary>Instances to select and zoom to after opening (empty = just open).</summary>
        public IReadOnlyList<long> ZoomInstanceIds { get; }
    }

    /// <summary>
    /// Opens views and selects / zooms to CAD instances. Must run in a Revit API context (the tool's
    /// external event) — Revit refuses these calls from a modeless window directly.
    /// V004: opens several views with options (zoom to the DWG in each, close every other open view).
    /// </summary>
    public static class DwgNavigationService
    {
        /// <summary>Makes <paramref name="viewId"/> the active view. Returns false if it cannot be opened.</summary>
        public static bool OpenView(UIDocument uidoc, long viewId, Action<LogEntry> log)
        {
            if (viewId == 0) return false;

            if (uidoc.Document.GetElement(new ElementId(viewId)) is not View view || !view.IsValidObject || view.IsTemplate)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning, $"View #{viewId} no longer exists. Refresh the statistics."));
                return false;
            }

            if (uidoc.ActiveView != null && uidoc.ActiveView.Id == view.Id) return true;

            try
            {
                uidoc.ActiveView = view;
                log?.Invoke(new LogEntry(LogLevel.Info, $"Opened: {view.Name}"));
                return true;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException ex)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning, $"Could not open '{view.Name}': {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// V004: opens each request in order (duplicates once); the last one opened ends up active.
        /// When a request has instances, they are selected and zoomed to right after that view opens.
        /// With <paramref name="closeOthers"/>, every other open view is closed afterwards.
        /// Returns how many views were opened.
        /// </summary>
        public static int OpenViews(UIDocument uidoc, IReadOnlyList<DwgOpenRequest> requests, bool closeOthers, Action<LogEntry> log)
        {
            int opened = 0;
            var openedIds = new HashSet<long>();

            foreach (var request in requests)
            {
                if (request.ViewId == 0 || openedIds.Contains(request.ViewId)) continue;
                if (!OpenView(uidoc, request.ViewId, log)) continue;

                openedIds.Add(request.ViewId);
                opened++;

                if (request.ZoomInstanceIds.Count > 0) Zoom(uidoc, request.ZoomInstanceIds, request.Label, log);
            }

            if (closeOthers && opened > 0) CloseOtherViews(uidoc, openedIds, log);
            return opened;
        }

        /// <summary>
        /// Selects instance <paramref name="instanceId"/> and zooms to it. If the active view does not
        /// draw it, <paramref name="fallbackViewId"/> (a view that does) is opened first.
        /// </summary>
        public static bool SelectAndZoom(UIDocument uidoc, long instanceId, long fallbackViewId, Action<LogEntry> log)
        {
            var doc = uidoc.Document;
            var id = new ElementId(instanceId);
            var element = doc.GetElement(id);
            if (element == null || !element.IsValidObject)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning, $"#{instanceId} no longer exists. Refresh the statistics."));
                return false;
            }

            if (!IsDrawnIn(doc, uidoc.ActiveView, id))
            {
                if (fallbackViewId == 0 || !OpenView(uidoc, fallbackViewId, log))
                {
                    log?.Invoke(new LogEntry(LogLevel.Warning, $"#{instanceId} is not drawn in any view that can be opened."));
                    return false;
                }
            }

            return Zoom(uidoc, new[] { instanceId }, $"#{instanceId}", log);
        }

        private static bool Zoom(UIDocument uidoc, IReadOnlyList<long> instanceIds, string label, Action<LogEntry> log)
        {
            var doc = uidoc.Document;
            var ids = instanceIds
                .Select(i => new ElementId(i))
                .Where(i => doc.GetElement(i) != null)
                .ToList();
            if (ids.Count == 0) return false;

            uidoc.Selection.SetElementIds(ids);
            try
            {
                uidoc.ShowElements(ids);
                log?.Invoke(new LogEntry(LogLevel.Info, $"Selected and zoomed to {ids.Count} instance(s) in {label}."));
                return true;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException ex)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning, $"Selected in {label} but could not zoom: {ex.Message}"));
                return true;
            }
        }

        private static void CloseOtherViews(UIDocument uidoc, HashSet<long> keep, Action<LogEntry> log)
        {
            int closed = 0;
            try
            {
                foreach (var uiView in uidoc.GetOpenUIViews().ToList())
                {
                    if (keep.Contains(uiView.ViewId.Value)) continue;
                    if (uidoc.ActiveView != null && uiView.ViewId == uidoc.ActiveView.Id) continue;
                    try
                    {
                        uiView.Close();
                        closed++;
                    }
                    catch (Autodesk.Revit.Exceptions.ApplicationException)
                    {
                        // Revit keeps some views open (e.g. the last view of a document); leave them.
                    }
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException ex)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning, $"Could not close the other views: {ex.Message}"));
                return;
            }

            if (closed > 0) log?.Invoke(new LogEntry(LogLevel.Info, $"Closed {closed} other open view(s)."));
        }

        private static bool IsDrawnIn(Document doc, View view, ElementId id)
        {
            if (view == null || !view.IsValidObject) return false;
            try
            {
                using (var collector = new FilteredElementCollector(doc, view.Id))
                    return collector.OfClass(typeof(ImportInstance)).ToElementIds().Contains(id);
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // Schedules, browsers…: nothing is drawn there.
            }
            return false;
        }
    }
}
