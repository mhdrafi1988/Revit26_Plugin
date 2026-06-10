using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using WorksetManager_01.Models;

namespace WorksetManager_01.Helpers
{
    public static class WorksetScanner
    {
        /// <summary>
        /// Scans all user worksets in the document.
        /// One FilteredElementCollector pass — groups results in memory.
        /// </summary>
        /// <param name="doc">Active Revit document.</param>
        /// <param name="includeLinkedModels">Whether to include elements from linked models.</param>
        public static List<WorksetSummaryItem> Scan(Document doc, bool includeLinkedModels)
        {
            // --- 1. Get all user worksets ---
            var wsFilter = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset);

            var worksets = wsFilter.ToWorksets().ToList();

            // Build a lookup: WorksetId (int) → WorksetSummaryItem
            var summaryMap = new Dictionary<int, WorksetSummaryItem>();
            foreach (var ws in worksets)
            {
                summaryMap[ws.Id.IntegerValue] = new WorksetSummaryItem
                {
                    WorksetId  = ws.Id.IntegerValue,
                    WorksetName = ws.Name,
                    IsEditable  = ws.IsEditable,
                    IsOpen      = ws.IsOpen,
                };
            }

            // --- 2. Single collector pass over all elements ---
            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType();

            foreach (Element elem in collector)
            {
                // Skip elements that belong to linked models unless toggled
                if (!includeLinkedModels && elem is RevitLinkInstance)
                    continue;

                WorksetId? wsId = elem.WorksetId;
                if (wsId == null) continue;

                int id = wsId.IntegerValue;
                if (!summaryMap.TryGetValue(id, out var summary)) continue;

                summary.TotalElements++;

                // Type name breakdown
                string typeName = GetTypeName(doc, elem);
                if (!summary.ByTypeName.ContainsKey(typeName))
                    summary.ByTypeName[typeName] = 0;
                summary.ByTypeName[typeName]++;
            }

            // Sort each workset's type breakdown by count descending
            foreach (var summary in summaryMap.Values)
            {
                summary.ByTypeName = summary.ByTypeName
                    .OrderByDescending(k => k.Value)
                    .ToDictionary(k => k.Key, k => k.Value);
            }

            return summaryMap.Values
                .OrderBy(s => s.WorksetName)
                .ToList();
        }

        private static string GetTypeName(Document doc, Element elem)
        {
            try
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    Element typeElem = doc.GetElement(typeId);
                    if (typeElem != null)
                        return typeElem.Name ?? "Unknown Type";
                }
            }
            catch { /* swallow — some elements don't have accessible types */ }

            return elem.GetType().Name;
        }
    }
}
