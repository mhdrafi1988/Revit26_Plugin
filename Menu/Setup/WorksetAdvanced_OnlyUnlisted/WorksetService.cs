using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Revit26_Plugin.WSAV03.Services
{
    public class WorksetServiceWSAV03
    {
        private readonly ObservableCollection<string> _log;

        public WorksetServiceWSAV03(ObservableCollection<string> logMessages)
        {
            _log = logMessages;
        }

        // Get clean file names
        public List<string> GetLinkedFileNames(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkType))
                .Cast<RevitLinkType>()
                .Select(t => Path.GetFileNameWithoutExtension(t.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // New method to check if a link is assigned to a workset
        public bool IsLinkAssignedToWorkset(Document doc, string linkName)
        {
            var instances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(li => li.GetLinkDocument()?.Title.Contains(linkName) == true)
                .ToList();

            if (!instances.Any())
                return false;

            // Check if any instance is assigned to a workset
            foreach (var inst in instances)
            {
                var param = inst.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (param != null && param.HasValue)
                {
                    int worksetId = param.AsInteger();
                    if (worksetId > 0) // Valid workset ID
                    {
                        // Verify the workset exists and is valid
                        Workset workset = null;
                        try
                        {
                            workset = doc.GetWorksetTable().GetWorkset(new WorksetId(worksetId));
                        }
                        catch
                        {
                            // Invalid workset ID
                        }

                        if (workset != null && !workset.IsDefaultWorkset)
                        {
                            _log.Add($"✓ Link '{linkName}' is assigned to workset: {workset.Name}");
                            return true;
                        }
                    }
                }
            }

            _log.Add($"✗ Link '{linkName}' is NOT assigned to any workset");
            return false;
        }

        // Create workset safely
        private WorksetId EnsureWorkset(Document doc, string worksetName)
        {
            var existing = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToList();

            var found = existing.FirstOrDefault(ws =>
                ws.Name.Equals(worksetName, StringComparison.OrdinalIgnoreCase));

            if (found != null)
            {
                _log.Add($"⚠ Workset exists: {worksetName}");
                return found.Id;
            }

            using (var tx = new Transaction(doc, "Create Workset"))
            {
                tx.Start();
                try
                {
                    var ws = Workset.Create(doc, worksetName);
                    tx.Commit();
                    _log.Add($"✔ Created: {worksetName}");
                    return ws.Id;
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    _log.Add($"❌ Failed creating '{worksetName}': {ex.Message}");
                    return null;
                }
            }
        }

        // Assign link to workset
        private void AssignLink(Document doc, string linkName, WorksetId wsId)
        {
            var instances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(li => li.GetLinkDocument()?.Title.Contains(linkName) == true)
                .ToList();

            if (!instances.Any())
            {
                _log.Add($"⚠ No link instances found for: {linkName}");
                return;
            }

            using (var tx = new Transaction(doc, "Assign Link Workset"))
            {
                tx.Start();
                try
                {
                    foreach (var inst in instances)
                    {
                        var param = inst.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                        param?.Set(wsId.IntegerValue);
                    }

                    tx.Commit();
                    _log.Add($"✔ Assigned '{linkName}' → Workset ID: {wsId.IntegerValue}");
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    _log.Add($"❌ Failed assigning '{linkName}': {ex.Message}");
                }
            }
        }

        // Update CreateAndAssign to return success status
        public bool CreateAndAssign(Document doc, string wsName, string linkName)
        {
            var wsId = EnsureWorkset(doc, wsName);
            if (wsId != null)
            {
                AssignLink(doc, linkName, wsId);
                return true;
            }
            return false;
        }

        // Update ReSyncAssignment to return success status
        public bool ReSyncAssignment(Document doc, string wsName, string linkName)
        {
            _log.Add($"🔄 Re-syncing {linkName}…");
            return CreateAndAssign(doc, wsName, linkName);
        }
    }
}