using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Revit26_Plugin.WorksetManager_02.Models;

namespace Revit26_Plugin.WorksetManager_02.Helpers
{
    public static class LinkScanner
    {
        /// <summary>
        /// Scans the document for RevitLinkInstances, matches them against
        /// worksets following the "+Link {basename}" pattern, and returns
        /// three categorised lists ready for the three grids.
        /// </summary>
        public static ScanResult Scan(Document doc)
        {
            // ── 1. Collect all user worksets ────────────────────────────────
            var allWorksets = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToWorksets()
                .ToList();

            // ── 2. Collect all RevitLinkInstance elements ───────────────────
            var linkInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();

            // Map: base filename (no ext, case‑insensitive) → list of instances
            var byBaseName = new Dictionary<string, List<RevitLinkInstance>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var inst in linkInstances)
            {
                string baseName = GetBaseName(doc, inst);
                if (string.IsNullOrWhiteSpace(baseName)) continue;

                if (!byBaseName.ContainsKey(baseName))
                    byBaseName[baseName] = new List<RevitLinkInstance>();
                byBaseName[baseName].Add(inst);
            }

            // ── 3. Categorise ───────────────────────────────────────────────
            var exactMatches = new List<ExactMatchItem>();
            var actionable = new List<LinkWorksetMatchItem>();
            var unmatched = new List<UnmatchedLinkItem>();

            foreach (var kvp in byBaseName.OrderBy(k => k.Key))
            {
                string baseName = kvp.Key;
                var instances = kvp.Value;
                string linkedFile = baseName + ".rvt";
                string proposed = "+Link " + baseName;

                Workset? matchedWs = allWorksets.FirstOrDefault(ws =>
                    ws.Name.IndexOf("+Link", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    ws.Name.IndexOf(baseName, StringComparison.OrdinalIgnoreCase) >= 0);

                if (matchedWs == null)
                {
                    unmatched.Add(new UnmatchedLinkItem
                    {
                        LinkedFileName = linkedFile,
                        ProposedWorkset = proposed,
                        InstanceCount = instances.Count
                    });
                }
                else
                {
                    int correctWsId = matchedWs.Id.IntegerValue;
                    int onCorrect = instances.Count(i => i.WorksetId?.IntegerValue == correctWsId);
                    int onWrong = instances.Count - onCorrect;

                    if (onWrong == 0)
                    {
                        exactMatches.Add(new ExactMatchItem
                        {
                            LinkedFileName = linkedFile,
                            MatchedWorkset = matchedWs.Name,
                            InstanceCount = instances.Count
                        });
                    }
                    else
                    {
                        actionable.Add(new LinkWorksetMatchItem
                        {
                            LinkedFileName = linkedFile,
                            MatchedWorkset = matchedWs.Name,
                            TotalInstances = instances.Count,
                            InstancesOnCorrectWorkset = onCorrect,
                            InstancesOnWrongWorkset = onWrong
                        });
                    }
                }
            }

            return new ScanResult
            {
                ExactMatches = exactMatches,
                ActionableLinks = actionable,
                UnmatchedLinks = unmatched,
                TotalWorksets = allWorksets.Count
            };
        }

        /// <summary>
        /// Reassigns all RevitLinkInstances for the given item to their matched workset.
        /// Must be called inside an open Transaction.
        /// Returns the number of instances successfully reassigned.
        /// </summary>
        public static int ReassignInstances(Document doc, LinkWorksetMatchItem item)
        {
            Workset? targetWs = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToWorksets()
                .FirstOrDefault(ws =>
                    ws.Name.IndexOf("+Link", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    ws.Name.IndexOf(GetBaseNameFromFile(item.LinkedFileName),
                                                       StringComparison.OrdinalIgnoreCase) >= 0);

            if (targetWs == null) return 0;

            var instances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(i => string.Equals(
                    GetBaseName(doc, i),
                    GetBaseNameFromFile(item.LinkedFileName),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            int count = 0;
            foreach (var inst in instances)
            {
                if (inst.WorksetId?.IntegerValue == targetWs.Id.IntegerValue) continue;

                // ✅ Correct method: Set the built-in workset parameter
                ChangeElementWorkset(inst, targetWs.Id);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Creates the "+Link {basename}" workset (if it doesn't exist),
        /// then assigns all matching instances to it.
        /// Must be called inside an open Transaction.
        /// Returns (worksetCreated, instancesAssigned).
        /// </summary>
        public static (bool worksetCreated, int instancesAssigned)
            CreateWorksetAndAssign(Document doc, UnmatchedLinkItem item)
        {
            string baseName = GetBaseNameFromFile(item.LinkedFileName);
            string wsName = item.ProposedWorkset;

            Workset? existing = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToWorksets()
                .FirstOrDefault(ws => string.Equals(ws.Name, wsName,
                    StringComparison.OrdinalIgnoreCase));

            WorksetId targetWsId;
            bool created = false;

            if (existing != null)
            {
                targetWsId = existing.Id;
            }
            else
            {
                Workset newWs = Workset.Create(doc, wsName);
                targetWsId = newWs.Id;
                created = true;
            }

            var instances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(i => string.Equals(
                    GetBaseName(doc, i), baseName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            int count = 0;
            foreach (var inst in instances)
            {
                if (inst.WorksetId?.IntegerValue == targetWsId.IntegerValue) continue;

                // ✅ Correct method: Set the built-in workset parameter
                ChangeElementWorkset(inst, targetWsId);
                count++;
            }

            return (created, count);
        }

        // ── Private helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Changes the workset of an element by setting the ELEM_PARTITION_PARAM parameter.
        /// Must be called inside an active transaction.
        /// </summary>
        private static void ChangeElementWorkset(Element element, WorksetId targetWorksetId)
        {
            Parameter worksetParam = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
            if (worksetParam == null)
                throw new InvalidOperationException($"Element {element.Id.Value} does not have a workset parameter.");

            worksetParam.Set(targetWorksetId.IntegerValue);
        }

        /// <summary>Gets the base filename (no extension) for a RevitLinkInstance.</summary>
        private static string GetBaseName(Document doc, RevitLinkInstance inst)
        {
            try
            {
                if (inst.GetTypeId() is ElementId typeId &&
                    typeId != ElementId.InvalidElementId &&
                    doc.GetElement(typeId) is RevitLinkType linkType)
                {
                    ExternalFileReference extRef = linkType.GetExternalFileReference();
                    if (extRef != null)
                    {
                        ModelPath modelPath = extRef.GetPath();
                        string path = ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
                        if (!string.IsNullOrWhiteSpace(path))
                            return Path.GetFileNameWithoutExtension(path);
                    }

                    string name = linkType.Name ?? string.Empty;
                    int colon = name.IndexOf(':');
                    if (colon > 0) name = name[..colon].Trim();
                    return Path.GetFileNameWithoutExtension(name);
                }
            }
            catch
            {
                // Swallow – fallback to inst.Name
            }

            return inst.Name ?? string.Empty;
        }

        private static string GetBaseNameFromFile(string linkedFileName)
            => Path.GetFileNameWithoutExtension(linkedFileName);
    }

    /// <summary>Aggregated result returned by LinkScanner.Scan.</summary>
    public class ScanResult
    {
        public List<ExactMatchItem> ExactMatches { get; set; } = new();
        public List<LinkWorksetMatchItem> ActionableLinks { get; set; } = new();
        public List<UnmatchedLinkItem> UnmatchedLinks { get; set; } = new();
        public int TotalWorksets { get; set; }
    }
}