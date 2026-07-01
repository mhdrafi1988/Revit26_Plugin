using Autodesk.Revit.DB;
using Revit26_Plugin.LinesFromMechanical.V010.Models;
using Revit26_Plugin.Shared.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.LinesFromMechanical.V010.Services;

/// <summary>
/// Pure Revit-query loading logic for the source-selection pickers
/// (links, families within a link, floor types grouped by family).
/// Holds no UI state — extracted from MainWindowViewModel so this logic
/// can be reused/tested independently of ObservableCollections.
/// </summary>
public sealed class LinkedMechanicalSourceLoader(Document doc, ViewPlan view)
{
    public delegate void TypedLogHandler(LogLevel level, string message);
    public event TypedLogHandler? OnLog;
    private void Log(LogLevel level, string message) => OnLog?.Invoke(level, message);

    public List<LinkInfo> LoadVisibleLinks()
    {
        var links = new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(RevitLinkInstance))
            .WhereElementIsNotElementType()
            .Cast<RevitLinkInstance>()
            .Where(l => l.GetLinkDocument() != null)
            .Select(link => new LinkInfo { Id = link.Id, Name = link.Name, Instance = link })
            .ToList();

        if (links.Count == 0)
            Log(LogLevel.Warning, "No visible loaded links found in active view.");

        return links;
    }

    public static List<string> LoadFamiliesForLink(RevitLinkInstance? link)
    {
        var linkDoc = link?.GetLinkDocument();
        if (linkDoc == null) return [];

        return new FilteredElementCollector(linkDoc)
            .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>()
            .Select(fi => fi.Symbol?.Family?.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .OrderBy(n => n)
            .Select(n => n!)
            .ToList();
    }

    public List<FloorFamilyInfo> LoadFloorFamilies(out bool noFloorTypesFound)
    {
        var floorTypes = new FilteredElementCollector(doc)
            .OfClass(typeof(FloorType))
            .Cast<FloorType>()
            .Where(ft => ft != null)
            .ToList();

        if (floorTypes.Count == 0)
        {
            noFloorTypesFound = true;
            Log(LogLevel.Warning, "No floor types found in document.");
            return [];
        }

        noFloorTypesFound = false;

        return floorTypes
            .GroupBy(GetFloorFamilyName)
            .OrderBy(g => g.Key)
            .Select(g => new FloorFamilyInfo { Name = g.Key, FloorTypes = g.ToList() })
            .ToList();
    }

    public static List<FloorType> LoadFloorTypesForFamily(FloorFamilyInfo? family)
        => family?.FloorTypes?.OrderBy(f => f.Name).ToList() ?? [];

    private static string GetFloorFamilyName(FloorType ft)
    {
        var p = ft.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME);
        return p != null && !string.IsNullOrEmpty(p.AsString()) ? p.AsString() : "Floor Types";
    }
}
