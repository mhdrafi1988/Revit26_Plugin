using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin_Revised.Shared.Models;

namespace Revit26_Plugin_Revised.DwgManager.V002.Core.Services
{
    /// <summary>
    /// Opens views and selects CAD instances in the Revit UI. Must run in a Revit API context
    /// (the tool's external event) — Revit refuses these calls from a modeless window directly.
    /// </summary>
    public static class DwgViewService
    {
        /// <summary>
        /// Opens each view in <paramref name="viewIds"/> (duplicates once); the last one opened ends up
        /// active. Returns how many views were opened.
        /// </summary>
        public static int OpenViews(UIDocument uidoc, IEnumerable<ElementId> viewIds, Action<LogEntry> log)
        {
            int opened = 0;
            var doc = uidoc.Document;

            foreach (var viewId in viewIds.Where(id => id != null && id != ElementId.InvalidElementId).Distinct())
            {
                if (doc.GetElement(viewId) is not View view || !view.IsValidObject || view.IsTemplate)
                {
                    log?.Invoke(new LogEntry(LogLevel.Warning, $"View #{viewId.Value} no longer exists; skipped."));
                    continue;
                }

                try
                {
                    uidoc.ActiveView = view;
                    opened++;
                    log?.Invoke(new LogEntry(LogLevel.Info, $"Opened view: {view.Name}"));
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException ex)
                {
                    log?.Invoke(new LogEntry(LogLevel.Warning, $"Could not open '{view.Name}': {ex.Message}"));
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
                {
                    log?.Invoke(new LogEntry(LogLevel.Warning, $"Could not open '{view.Name}': {ex.Message}"));
                }
            }

            return opened;
        }

        /// <summary>Selects one CAD instance and zooms the active view to it.</summary>
        public static bool SelectAndZoom(UIDocument uidoc, ElementId id, Action<LogEntry> log)
        {
            var element = uidoc.Document.GetElement(id);
            if (element == null || !element.IsValidObject)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning, $"#{id.Value} no longer exists. Refresh the list."));
                return false;
            }

            uidoc.Selection.SetElementIds(new List<ElementId> { id });

            try
            {
                uidoc.ShowElements(id);
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning, $"Selected #{id.Value} but could not zoom to it: {ex.Message}"));
                return true;
            }

            log?.Invoke(new LogEntry(LogLevel.Info, $"Selected and zoomed to #{id.Value}."));
            return true;
        }
    }
}
