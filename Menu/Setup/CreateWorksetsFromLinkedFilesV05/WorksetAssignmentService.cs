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

        public static string GetCleanLinkName(RevitLinkType type)
        {
            ExternalFileReference ext = type.GetExternalFileReference();
            if (ext == null)
                return type.Name;

            string userPath =
                ModelPathUtils.ConvertModelPathToUserVisiblePath(ext.GetPath());

            return Path.GetFileNameWithoutExtension(userPath);
        }

        public Workset GetOrCreateWorkset(Document doc, string name)
        {
            Workset ws = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .FirstOrDefault(w =>
                    w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (ws != null)
            {
                _log.Add($"⏭ Workset exists: {name}");
                return ws;
            }

            ws = Workset.Create(doc, name);
            _log.Add($"✔ Created: {name}");
            return ws;
        }

        public void Assign(
            Document doc,
            RevitLinkType type,
            string worksetName)
        {
            var instances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(i => i.GetTypeId() == type.Id)
                .ToList();

            if (!instances.Any())
            {
                _log.Add($"⚠ No instances found: {worksetName}");
                return;
            }

            Workset ws = GetOrCreateWorkset(doc, worksetName);

            foreach (var inst in instances)
            {
                Parameter p =
                    inst.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);

                if (p == null)
                    continue;

                ElementId current = p.AsElementId();

                // ✅ Revit 2026–correct comparison
                if (current != null && current.Value == ws.Id.IntegerValue)
                {
                    _log.Add($"⏭ Already correct: {worksetName}");
                    continue;
                }

                p.Set(new ElementId(ws.Id.IntegerValue));
                _log.Add($"✔ Assigned → {worksetName}");
            }
        }
    }
}
