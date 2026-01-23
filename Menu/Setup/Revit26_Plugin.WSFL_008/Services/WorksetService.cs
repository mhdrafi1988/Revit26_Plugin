using Autodesk.Revit.DB;
using Revit26_Plugin.WSFL_008.ViewModels;
using Revit26_Plugin.WSFL_008.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Revit26_Plugin.WSFL_008.Services
{
    public class WorksetService
    {
        private readonly Action<LogEntry> _log;

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

        public void CreateAndAssign(
            Document doc,
            IEnumerable<(string WorksetName, string LinkName)> items)
        {
            using Transaction tx =
                new Transaction(doc, "WSFL – Create & Assign Worksets");

            try
            {
                tx.Start();

                foreach (var (worksetName, linkName) in items)
                {
                    WorksetId wsId = EnsureWorkset(doc, worksetName);
                    AssignLinkInstances(doc, linkName, wsId);
                }

                tx.Commit();
                _log(new LogEntry(LogLevel.Info, "Operation completed successfully."));
            }
            catch (Exception ex)
            {
                tx.RollBack();
                _log(new LogEntry(LogLevel.Error, ex.Message));
            }
        }

        private WorksetId EnsureWorkset(Document doc, string name)
        {
            Workset existing = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .FirstOrDefault(ws =>
                    ws.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                _log(new LogEntry(LogLevel.Warning, $"Workset already exists: {name}"));
                return existing.Id;
            }

            Workset created = Workset.Create(doc, name);
            _log(new LogEntry(LogLevel.Info, $"Created workset: {name}"));
            return created.Id;
        }

        private void AssignLinkInstances(
            Document doc,
            string linkName,
            WorksetId worksetId)
        {
            var instances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(i =>
                    i.GetLinkDocument()?.Title
                        .Contains(linkName, StringComparison.OrdinalIgnoreCase) == true);

            foreach (RevitLinkInstance instance in instances)
            {
                instance
                    .get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM)
                    ?.Set(worksetId.IntegerValue);
            }

            _log(new LogEntry(LogLevel.Info, $"Assigned links: {linkName}"));
        }
    }
}
