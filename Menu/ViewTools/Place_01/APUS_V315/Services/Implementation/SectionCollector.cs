using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using Revit26_Plugin.APUS_V315.ViewModels.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V315.Services.Implementation;

public sealed class SectionCollector : ISectionCollector
{
    private readonly IViewSizeCalculator _sizeCalculator;

    public SectionCollector(IViewSizeCalculator sizeCalculator)
    {
        _sizeCalculator = sizeCalculator ?? throw new ArgumentNullException(nameof(sizeCalculator));
    }

    public IReadOnlyList<SectionItemViewModel> Collect(Document document)
    {
        if (document == null)
            return Array.Empty<SectionItemViewModel>();

        var result = new List<SectionItemViewModel>();

        // Get all viewports
        var viewports = new FilteredElementCollector(document)
            .OfClass(typeof(Viewport))
            .Cast<Viewport>()
            .ToList();

        // Create sheet lookup
        var viewToSheetMap = CreateViewToSheetMap(document, viewports);

        // Get all section views
        var sections = new FilteredElementCollector(document)
            .OfClass(typeof(ViewSection))
            .Cast<ViewSection>()
            .Where(v => !v.IsTemplate &&
                       v.ViewType == ViewType.Section &&
                       v.GetPrimaryViewId() == ElementId.InvalidElementId)
            .ToList();

        foreach (var section in sections)
        {
            try
            {
                bool isPlaced = viewToSheetMap.TryGetValue(section.Id, out var sheet);
                string sheetNumber = sheet?.SheetNumber ?? string.Empty;

                var placementScope = section.LookupParameter("Placement_Scope")?.AsString() ?? string.Empty;

                result.Add(new SectionItemViewModel(
                    section,
                    isPlaced,
                    sheetNumber,
                    placementScope,
                    _sizeCalculator));
            }
            catch
            {
                // Skip problematic sections
            }
        }

        return result.OrderBy(s => s.ViewName).ToList();
    }

    private static Dictionary<ElementId, ViewSheet> CreateViewToSheetMap(
        Document document,
        List<Viewport> viewports)
    {
        var map = new Dictionary<ElementId, ViewSheet>();

        var sheets = new FilteredElementCollector(document)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .ToList();

        foreach (var sheet in sheets)
        {
            try
            {
                foreach (var viewId in sheet.GetAllPlacedViews())
                {
                    if (!map.ContainsKey(viewId))
                        map[viewId] = sheet;
                }
            }
            catch
            {
                // Skip problematic sheets
            }
        }

        foreach (var viewport in viewports)
        {
            try
            {
                var viewId = viewport.ViewId;
                if (!map.ContainsKey(viewId))
                {
                    var sheet = document.GetElement(viewport.OwnerViewId) as ViewSheet;
                    if (sheet != null)
                        map[viewId] = sheet;
                }
            }
            catch
            {
                // Skip problematic viewports
            }
        }

        return map;
    }
}