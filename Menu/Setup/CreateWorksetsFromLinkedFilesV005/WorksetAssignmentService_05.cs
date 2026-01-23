// WorksetAssignmentService.cs - Full updated version with transaction-safe workset creation
using Autodesk.Revit.DB;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Revit26_Plugin.WSA_V05.Services
{
    public class WorksetAssignmentService
    {
        private readonly ObservableCollection<string> _log;

        public WorksetAssignmentService(ObservableCollection<string> log)
        {
            _log = log;
        }

        /// <summary>
        /// Retrieves the filename of the link. Uses Revit 2026 compliant status checks.
        /// </summary>
        public static string GetCleanLinkName(RevitLinkType type)
        {
            ExternalFileReference ext = type.GetExternalFileReference();
            if (ext == null) return type.Name;

            try
            {
                string userPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(ext.GetPath());
                return Path.GetFileNameWithoutExtension(userPath);
            }
            catch
            {
                return type.Name;
            }
        }

        /// <summary>
        /// Gets existing workset or creates new one with proper transaction handling
        /// </summary>
        public Workset GetOrCreateWorkset(Document doc, string name)
        {
            // Check if workset already exists
            Workset ws = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (ws != null)
            {
                _log.Add($"[FOUND] Workset: {name}");
                return ws;
            }

            // Create new workset with transaction
            using (var tx = new Transaction(doc, "Create Workset"))
            {
                tx.Start();
                try
                {
                    ws = Workset.Create(doc, name);
                    tx.Commit();
                    _log.Add($"[NEW] Workset: {name}");
                    return ws;
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    _log.Add($"❌ Failed creating workset '{name}': {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Assigns link instances to a specific workset
        /// </summary>
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
                    _log.Add($"✔ Assigned '{linkName}' → Workset ID '{wsId.IntegerValue}'");
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    _log.Add($"❌ Failed assigning '{linkName}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Main public method to assign a RevitLinkType to a workset
        /// </summary>
        public void Assign(Document doc, RevitLinkType type, string worksetName)
        {
            string linkName = GetCleanLinkName(type);
            Workset ws = GetOrCreateWorkset(doc, worksetName);

            if (ws == null)
            {
                _log.Add($"❌ Cannot assign '{linkName}': Workset creation failed");
                return;
            }

            _log.Add($"[ASSIGN] Attempting '{linkName}' → '{worksetName}'");
            AssignLink(doc, linkName, ws.Id);
        }
    }
}
