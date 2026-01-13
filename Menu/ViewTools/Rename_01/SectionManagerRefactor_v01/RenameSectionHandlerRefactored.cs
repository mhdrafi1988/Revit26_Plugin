using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.SectionManagerMVVM_Refactored
{
    public class RenameSectionHandlerRefactored : IExternalEventHandler
    {
        public List<SectionViewModelRefactored> PayloadList { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            int renamed = 0;
            int skipped = 0;
            int failed = 0;

            string[] bannedNames = { "CON", "PRN", "AUX", "NUL" };

            using (Transaction tx = new Transaction(doc, "Rename Sections"))
            {
                tx.Start();

                foreach (var vm in PayloadList)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(vm.PreviewName))
                        {
                            TaskDialog.Show("Invalid Name", $"Name cannot be empty for section '{vm.OriginalName}'.");
                            skipped++;
                            continue;
                        }

                        string baseName = vm.PreviewName.Trim();
                        if (bannedNames.Contains(baseName.ToUpperInvariant()))
                        {
                            TaskDialog.Show("Reserved Name", $"'{baseName}' is a reserved name and cannot be used.");
                            failed++;
                            continue;
                        }

                        Element sectionEl = doc.GetElement(vm.ElementId);
                        if (!(sectionEl is ViewSection section))
                        {
                            failed++;
                            continue;
                        }

                        if (section.Pinned)
                            section.Pinned = false;

                        string finalName = vm.PreviewName; // ✅ use preview name as-is

                        if (section.Name != finalName)
                        {
                            vm.OldName = section.Name;
                            section.Name = finalName;
                            vm.PreviewName = finalName;
                            renamed++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Rename Error", $"Failed to rename section '{vm.OriginalName}' to '{vm.PreviewName}':\n{ex.Message}");
                        failed++;
                    }
                }

                tx.Commit();
            }

            TaskDialog.Show("Rename Summary", $"Renamed: {renamed}\nSkipped: {skipped}\nFailed: {failed}");
        }

        public string GetName() => nameof(RenameSectionHandlerRefactored);
    }
}
