using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionAutoRenamer._01.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revit26_Plugin.SectionAutoRenamer._01.Events;

public class RenameSectionsHandler : IExternalEventHandler
{
    public List<SectionItemViewModel> Payload { get; set; } = new();
    public SectionsListViewModel      Vm      { get; set; } = null!;

    public void Execute(UIApplication app)
    {
        var doc = app.ActiveUIDocument.Document;

        // Snapshot of all current section names — used to avoid collisions.
        var existingNames = new HashSet<string>(
            new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => !v.IsTemplate)
                .Select(v => v.Name),
            StringComparer.OrdinalIgnoreCase);

        int ok = 0, fixedDup = 0, fail = 0;

        using var tx = new Transaction(doc, "SAR01 Rename Sections");
        tx.Start();

        foreach (var s in Payload)
        {
            try
            {
                var v = doc.GetElement(s.ElementId) as ViewSection;
                if (v == null) continue;

                // Un-pin silently if needed, but log a warning per view
                if (v.Pinned)
                {
                    v.Pinned = false;
                    Dispatch(() => Vm.LogWarning($"\"{s.OriginalName}\" was pinned — un-pinned automatically."));
                }

                // Remove the element's current name so it doesn't block its own rename
                // (Bug 6 fix carried forward from V07)
                existingNames.Remove(v.Name);

                string finalName = s.PreviewName;
                if (existingNames.Contains(finalName))
                {
                    finalName = GenerateUniqueName(finalName, existingNames, Vm.DuplicateStrategy);
                    fixedDup++;
                }

                v.Name = finalName;
                existingNames.Add(finalName);
                ok++;
            }
            catch (Exception ex)
            {
                fail++;
                Dispatch(() => Vm.LogError($"\"{s.OriginalName}\": {ex.Message}"));
            }
        }

        tx.Commit();

        Dispatch(() => Vm.LogSuccess(
            $"Rename complete — OK: {ok}  |  Duplicates fixed: {fixedDup}  |  Failed: {fail}"));
    }

    // ── Dispatcher marshal (handler runs on Revit API thread) ────────────────
    private static void Dispatch(Action action) =>
        System.Windows.Application.Current?.Dispatcher.Invoke(action);

    // ── Unique-name generator ─────────────────────────────────────────────
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
                DuplicateFixStrategy.AlphabetSuffix   => $"{baseName}-{ToAlpha(i)}",
                DuplicateFixStrategy.DupSuffix         => i == 1 ? $"{baseName}_dup" : $"{baseName}_dup{i}",
                _                                      => $"{baseName} ({i})"
            };
            if (!existing.Contains(candidate)) return candidate;
            i++;
        }
    }

    // 1→A, 26→Z, 27→AA … (no char-overflow past Z)
    private static string ToAlpha(int n)
    {
        var sb = new StringBuilder();
        while (n > 0) { n--; sb.Insert(0, (char)('A' + n % 26)); n /= 26; }
        return sb.ToString();
    }

    public string GetName() => "SAR01 Rename Handler";
}
