using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SARV6.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.SARV6.ViewModels;


namespace Revit26_Plugin.SARV6.Events;

public class RenameSectionsHandler : IExternalEventHandler
{
    public List<SectionItemViewModel> Payload { get; set; }
    public SectionsListViewModel Vm { get; set; }

    public void Execute(UIApplication app)
    {
        var doc = app.ActiveUIDocument.Document;

        using var tg = new TransactionGroup(doc, "SARV6 Rename Sections");
        tg.Start();

        var existingNames = new HashSet<string>(
            new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => !v.IsTemplate)
                .Select(v => v.Name),
            StringComparer.OrdinalIgnoreCase);

        int ok = 0, fixedDup = 0, fail = 0;

        using (var tx = new Transaction(doc, "Rename Sections"))
        {
            tx.Start();

            foreach (var s in Payload)
            {
                try
                {
                    var v = doc.GetElement(s.ElementId) as ViewSection;
                    if (v == null) continue;

                    if (v.Pinned) v.Pinned = false;

                    string finalName = s.PreviewName;

                    if (existingNames.Contains(finalName))
                    {
                        finalName = GenerateUniqueName(
                            finalName,
                            existingNames,
                            Vm.DuplicateStrategy);
                        fixedDup++;
                    }

                    v.Name = finalName;
                    existingNames.Add(finalName);
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    Vm.LogError($"{s.OriginalName}: {ex.Message}");
                }
            }

            tx.Commit();
        }

        tg.Assimilate();
        Vm.LogSuccess(
            $"Rename complete → Success: {ok}, Duplicates fixed: {fixedDup}, Failed: {fail}");
    }

    private static string GenerateUniqueName(
        string baseName,
        HashSet<string> existing,
        DuplicateFixStrategy strategy)
    {
        int i = 1;

        while (true)
        {
            string candidate = strategy switch
            {
                DuplicateFixStrategy.NumberedBrackets => $"{baseName} ({i})",
                DuplicateFixStrategy.AlphabetSuffix => $"{baseName}-{(char)('A' + i - 1)}",
                DuplicateFixStrategy.DupSuffix => i == 1 ? $"{baseName}_dup" : $"{baseName}_dup{i}",
                _ => $"{baseName} ({i})"
            };

            if (!existing.Contains(candidate))
                return candidate;

            i++;
        }
    }

    public string GetName() => "SARV6 Rename Handler";
}
