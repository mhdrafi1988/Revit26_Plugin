using Autodesk.Revit.DB;
using Revit26_Plugin_Revised.ViewAutoRenamer.V005.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin_Revised.ViewAutoRenamer.V005.Services;

/// <summary>
/// Sheet-placement facts for one view, built once per Load pass.
/// Replaces the old per-view get_Parameter(VIEWER_SHEET_NUMBER) /
/// (VIEWER_DETAIL_NUMBER) reads — both values now come out of the same
/// Viewport sweep, so a load costs 3 collectors total instead of
/// 2-per-sheet collectors plus 2 parameter lookups per view.
/// </summary>
public sealed class ViewPlacement
{
    public List<string> SheetNumbers { get; } = new();
    public string? DetailNumber { get; set; }
}

/// <summary>
/// View classification and sheet-placement lookup — business logic, kept
/// out of command orchestration.
/// </summary>
public static class ViewClassificationService
{
    /// <summary>
    /// Exact ViewType values this tool handles, per confirmed requirements:
    /// Sections &amp; Callouts, Plans (Floor/Ceiling/Structural/Area),
    /// Elevations, Drafting, Legends, Schedules.
    ///
    /// NOTE: Callouts of sections report ViewType.Section (no separate
    /// ViewType exists for them) and are included automatically.
    /// </summary>
    public static readonly IReadOnlyCollection<ViewType> InScopeViewTypes = new HashSet<ViewType>
    {
        ViewType.Section,
        ViewType.Elevation,
        ViewType.FloorPlan,
        ViewType.CeilingPlan,
        ViewType.EngineeringPlan,   // Structural Plan
        ViewType.AreaPlan,
        ViewType.DraftingView,
        ViewType.Legend,
        ViewType.Schedule,
    };

    /// <summary>
    /// ViewType -> (duplicate-name group, display label), without needing a
    /// View instance. The type-picker is built from a cheap scan that never
    /// materialises row view-models, so classification can't depend on one.
    ///
    /// Callouts report ViewType.Section in the Revit API; distinguishing a
    /// callout from a plain section means walking dependent elements
    /// (fragile). Per confirmed decision both are labeled "Section" and
    /// share one duplicate-name group (SectionOrCallout), which matches
    /// Revit's actual name-uniqueness rule regardless of the label.
    /// </summary>
    public static bool TryClassify(ViewType viewType, out ViewTypeGroup group, out string display)
    {
        switch (viewType)
        {
            case ViewType.Section:         group = ViewTypeGroup.SectionOrCallout; display = "Section";          return true;
            case ViewType.Elevation:       group = ViewTypeGroup.Elevation;        display = "Elevation";        return true;
            case ViewType.FloorPlan:       group = ViewTypeGroup.FloorPlan;        display = "Floor Plan";       return true;
            case ViewType.CeilingPlan:     group = ViewTypeGroup.CeilingPlan;      display = "Ceiling Plan";     return true;
            case ViewType.EngineeringPlan: group = ViewTypeGroup.StructuralPlan;   display = "Structural Plan";  return true;
            case ViewType.AreaPlan:        group = ViewTypeGroup.AreaPlan;         display = "Area Plan";        return true;
            case ViewType.DraftingView:    group = ViewTypeGroup.Drafting;         display = "Drafting View";    return true;
            case ViewType.Legend:          group = ViewTypeGroup.Legend;           display = "Legend";           return true;
            case ViewType.Schedule:        group = ViewTypeGroup.Schedule;         display = "Schedule";         return true;
            default:                       group = default;                        display = "";                 return false;
        }
    }

    /// <summary>
    /// Strict variant for callers that have already filtered to the in-scope
    /// set — fails loudly rather than silently mis-bucketing an unexpected
    /// view type into an unrelated duplicate-name group.
    /// </summary>
    public static (ViewTypeGroup group, string display) ClassifyView(View v)
    {
        if (TryClassify(v.ViewType, out var group, out var display))
            return (group, display);

        throw new System.InvalidOperationException(
            $"Unhandled ViewType '{v.ViewType}' reached ClassifyView — " +
            "update InScopeViewTypes and TryClassify together if a new type was added.");
    }

    /// <summary>
    /// Cheap pre-load scan: how many in-scope views of each ViewType exist in
    /// the model. Reads ONLY IsTemplate and ViewType per element — no Name,
    /// no parameters, no sheet lookup — so the window can open immediately
    /// and let the user choose what to actually load.
    /// </summary>
    public static Dictionary<ViewType, int> ScanViewTypeCounts(Document doc)
    {
        var counts = new Dictionary<ViewType, int>();

        var collector = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .WhereElementIsNotElementType();

        foreach (var e in collector)
        {
            if (e is not View v || v.IsTemplate) continue;

            var vt = v.ViewType;
            if (!InScopeViewTypes.Contains(vt)) continue;

            counts.TryGetValue(vt, out var n);
            counts[vt] = n + 1;
        }

        return counts;
    }

    /// <summary>
    /// Builds ViewId -> placement (sheet numbers + detail number) for the
    /// whole document using THREE document-wide collectors.
    ///
    /// The previous implementation created two scoped collectors per sheet
    /// (FilteredElementCollector(doc, sheet.Id) for Viewport and for
    /// ScheduleSheetInstance) — 600+ collectors on a 300-sheet model, and
    /// the single largest cost of opening the tool. One doc-wide pass over
    /// each class gives the same data; Viewport.SheetId /
    /// ScheduleSheetInstance.OwnerViewId supply the sheet each instance sits
    /// on.
    ///
    /// Viewports cover plans, sections, callouts, elevations, drafting views
    /// AND legends — so every in-scope type except schedules gets its sheet
    /// number here, and the detail number comes off the same Viewport. Only
    /// schedules need the second sweep.
    /// </summary>
    public static Dictionary<ElementId, ViewPlacement> BuildPlacementLookup(Document doc)
    {
        var result = new Dictionary<ElementId, ViewPlacement>();

        ViewPlacement Slot(ElementId viewId)
        {
            if (!result.TryGetValue(viewId, out var p))
            {
                p = new ViewPlacement();
                result[viewId] = p;
            }
            return p;
        }

        void AddSheet(ElementId viewId, string? sheetNumber)
        {
            if (viewId == ElementId.InvalidElementId || string.IsNullOrWhiteSpace(sheetNumber)) return;
            var p = Slot(viewId);
            if (!p.SheetNumbers.Contains(sheetNumber!)) p.SheetNumbers.Add(sheetNumber!);
        }

        // 1 of 3 — sheet Id -> sheet number, so the sweeps below never have
        // to re-fetch a ViewSheet element per instance.
        var sheetNumberById = new Dictionary<ElementId, string>();
        foreach (var e in new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)))
        {
            if (e is ViewSheet sheet)
                sheetNumberById[sheet.Id] = sheet.SheetNumber;
        }

        // 2 of 3 — every Viewport in the document.
        foreach (var e in new FilteredElementCollector(doc).OfClass(typeof(Viewport)))
        {
            if (e is not Viewport vp) continue;
            if (!sheetNumberById.TryGetValue(vp.SheetId, out var sheetNumber)) continue;

            AddSheet(vp.ViewId, sheetNumber);

            // Detail number lives on the Viewport, not the View — one lookup
            // per PLACED viewport instead of one per view in the model.
            if (vp.ViewId != ElementId.InvalidElementId)
            {
                var detail = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)?.AsString();
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    var p = Slot(vp.ViewId);
                    p.DetailNumber ??= detail;
                }
            }
        }

        // 3 of 3 — schedules are placed via ScheduleSheetInstance, not Viewport.
        foreach (var e in new FilteredElementCollector(doc).OfClass(typeof(ScheduleSheetInstance)))
        {
            if (e is not ScheduleSheetInstance si) continue;
            if (!sheetNumberById.TryGetValue(si.OwnerViewId, out var sheetNumber)) continue;

            // Titleblock revision schedules report ElementId.InvalidElementId
            // here and are dropped by AddSheet's guard, same as before.
            AddSheet(si.ScheduleId, sheetNumber);
        }

        return result;
    }
}
