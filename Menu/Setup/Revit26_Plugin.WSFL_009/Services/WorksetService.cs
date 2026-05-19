using Autodesk.Revit.DB;
using Revit26_Plugin.WSFL_009.Models;
using Revit26_Plugin.WSFL_009.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Revit26_Plugin.WSFL_009.Services
{
    public class WorksetService
    {
        private readonly Action<LogEntry> _log;
        private int _processedCount;

        public WorksetService(Action<LogEntry> logger)
        {
            _log = logger;
        }

        public List<string> GetLinkedFileNames(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkType))
                .Cast<RevitLinkType>()
                .Select(t => Path.GetFileNameWithoutExtension(t.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool HasInstances(Document doc, string linkName)
        {
            var instances = GetLinkInstances(doc, linkName);
            return instances.Any();
        }

        public string GetCurrentWorksetName(Document doc, string linkName)
        {
            var instances = GetLinkInstances(doc, linkName);

            if (!instances.Any())
                return "No instances";

            var worksetIds = new HashSet<int>();
            var worksetNames = new HashSet<string>();

            foreach (var instance in instances)
            {
                var param = instance.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (param != null && param.HasValue)
                {
                    int wsId = param.AsInteger();
                    worksetIds.Add(wsId);

                    Workset workset = new FilteredWorksetCollector(doc)
                        .FirstOrDefault(ws => ws.Id.IntegerValue == wsId);

                    if (workset != null)
                        worksetNames.Add(workset.Name);
                }
            }

            if (worksetNames.Count == 0)
                return "None";
            if (worksetNames.Count == 1)
                return worksetNames.First();

            return "MIXED";
        }

        public string CheckExistingWorkset(Document doc, string worksetName)
        {
            Workset existing = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .FirstOrDefault(ws =>
                    ws.Name.Equals(worksetName, StringComparison.OrdinalIgnoreCase));

            return existing?.Name;
        }

        public void ResolveDuplicates(IEnumerable<WorksetItem> items)
        {
            var usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var itemsToProcess = items.Where(i => !i.IsExistingWorkset).ToList();

            foreach (var item in itemsToProcess)
            {
                string baseName = item.ProposedWorksetName;
                string finalName = baseName;
                int count = 1;

                while (usedNames.ContainsKey(finalName))
                {
                    count++;
                    finalName = $"{baseName}_{count}";
                }

                if (finalName != baseName)
                {
                    _log(new LogEntry(LogLevel.Warning, $"Duplicate name '{baseName}' → renamed to '{finalName}'"));
                    item.ProposedWorksetName = finalName;
                }

                usedNames[finalName] = count;
            }
        }

        public string CleanName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "Workset";

            string original = input;
            string cleaned = input;

            // Invalid characters for Revit worksets
            char[] invalidChars = { ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~', '\t', '\n', '\r' };

            foreach (char c in invalidChars)
            {
                cleaned = cleaned.Replace(c, '-');
            }

            // Replace multiple consecutive hyphens with single hyphen
            while (cleaned.Contains("--"))
            {
                cleaned = cleaned.Replace("--", "-");
            }

            // Trim leading/trailing spaces and hyphens
            cleaned = cleaned.Trim().Trim('-');

            // If result is empty, use default
            if (string.IsNullOrEmpty(cleaned))
            {
                cleaned = "Workset";
            }

            // Enforce 32 character limit
            if (cleaned.Length > 32)
            {
                string truncated = cleaned.Substring(0, 32);
                _log(new LogEntry(LogLevel.Warning, $"Truncated '{cleaned}' to 32 chars: '{truncated}'"));
                cleaned = truncated;
            }

            if (original != cleaned)
            {
                _log(new LogEntry(LogLevel.Info, $"Cleaned name: '{original}' → '{cleaned}'"));
            }

            return cleaned;
        }

        public void CreateAndAssign(Document doc, IEnumerable<(string ProposedName, string LinkName)> items, WorksetsViewModel viewModel)
        {
            _processedCount = 0;
            int total = items.Count();

            foreach (var (proposedName, linkName) in items)
            {
                _processedCount++;

                using (Transaction tx = new Transaction(doc, $"Create workset: {proposedName}"))
                {
                    try
                    {
                        tx.Start();

                        // Create workset (already validated that it doesn't exist)
                        Workset workset = Workset.Create(doc, proposedName);
                        _log(new LogEntry(LogLevel.Info, $"Created workset: '{proposedName}'"));

                        // Assign instances
                        int assignedCount = AssignLinkInstances(doc, linkName, workset.Id);
                        _log(new LogEntry(LogLevel.Info, $"Assigned {assignedCount} instances of '{linkName}' to workset '{proposedName}'"));

                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        _log(new LogEntry(LogLevel.Error, $"Failed to create workset '{proposedName}': {ex.Message}"));
                        // Continue with next workset
                    }
                }

                // Keep UI responsive after every 5 worksets
                if (_processedCount % 5 == 0)
                {
                    viewModel?.KeepUIResponsive();
                }
            }
        }

        private int AssignLinkInstances(Document doc, string linkName, WorksetId worksetId)
        {
            var instances = GetLinkInstances(doc, linkName);
            int count = 0;

            foreach (RevitLinkInstance instance in instances)
            {
                instance
                    .get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM)
                    ?.Set(worksetId.IntegerValue);
                count++;
            }

            return count;
        }

        private List<RevitLinkInstance> GetLinkInstances(Document doc, string linkName)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(i =>
                {
                    var linkDoc = i.GetLinkDocument();
                    if (linkDoc == null) return false;
                    string title = Path.GetFileNameWithoutExtension(linkDoc.Title);
                    return title.Equals(linkName, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();
        }
    }
}