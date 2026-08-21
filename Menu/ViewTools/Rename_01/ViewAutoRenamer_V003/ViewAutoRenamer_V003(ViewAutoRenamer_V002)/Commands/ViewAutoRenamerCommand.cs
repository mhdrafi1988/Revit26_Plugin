using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.ViewAutoRenamer.V003.Models;
using Revit26_Plugin.ViewAutoRenamer.V003.Services;
using Revit26_Plugin.ViewAutoRenamer.V003.ViewModels;
using Revit26_Plugin.ViewAutoRenamer.V003.Views;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.ViewAutoRenamer.V003.Commands;

[Transaction(TransactionMode.Manual)]
public class OpenViewAutoRenamerCommand : IExternalCommand
{
    // Exact ViewType values in scope, per confirmed requirements:
    // Sections & Callouts, Plans (Floor/Ceiling/Structural/Area), Elevations,
    // Drafting, Legends, Schedules.
    private static readonly HashSet<ViewType> InScopeViewTypes = new()
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
    // NOTE: Callouts of sections report ViewType.Section (no separate
    // ViewType exists for them) and are included automatically by this
    // filter. See ClassifyView for the SectionOrCallout grouping rationale.

    public Result Execute(ExternalCommandData c, ref string m, ElementSet e)
    {
        RevitEventManager.Initialize();

        var uidoc = c.Application.ActiveUIDocument;
        var doc   = uidoc.Document;

        var activeSheet       = uidoc.ActiveView as ViewSheet;
        string activeSheetNum = activeSheet?.SheetNumber ?? "";

        // ── Build sheet-placement lookup for Legend/Schedule (can appear on
        // multiple sheets — ViewSheet.GetAllPlacedViews / ScheduleSheetInstance) ──
        var placedSheetsByViewId = BuildPlacedSheetsLookup(doc);

        // ── Collect all in-scope views ──────────────────────────────────────
        var allViews = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate && InScopeViewTypes.Contains(v.ViewType))
            .ToList();

        var items = new List<ViewItemViewModel>();
        foreach (var v in allViews)
        {
            var (group, display) = ClassifyView(v);

            IReadOnlyList<string> placedSheets;
            string? detailNumber = null;

            if (v.ViewType == ViewType.Legend || v.ViewType == ViewType.Schedule)
            {
                // Legends/Schedules: look up from the multi-sheet lookup table.
                placedSheets = placedSheetsByViewId.TryGetValue(v.Id, out var list)
                    ? list
                    : new List<string>();
            }
            else
            {
                // Sections/Callouts/Elevations/Plans/Drafting: single placement
                // via VIEWER_SHEET_NUMBER, same approach as V012.
                var sheetNum = v.get_Parameter(BuiltInParameter.VIEWER_SHEET_NUMBER)?.AsString();
                detailNumber = v.get_Parameter(BuiltInParameter.VIEWER_DETAIL_NUMBER)?.AsString();
                placedSheets = string.IsNullOrWhiteSpace(sheetNum)
                    ? new List<string>()
                    : new List<string> { sheetNum };
            }

            items.Add(new ViewItemViewModel(v, group, display, placedSheets, detailNumber));
        }

        var vm = new ViewsListViewModel(items, activeSheetNum);
        new ViewsListWindow(vm).Show();

        return Result.Succeeded;
    }

    /// <summary>
    /// Classifies a view into its ViewTypeGroup (for dup-check + filter) and
    /// a human display label (for the grid pill/column).
    ///
    /// NOTE: Callouts report ViewType.Section in the Revit API — there is no
    /// separate ViewType for them, and reliably distinguishing a callout
    /// from a plain section requires walking dependent elements (fragile).
    /// Per confirmed decision, both are labeled "Section" and share one
    /// duplicate-name group (SectionOrCallout), which matches Revit's
    /// actual name-uniqueness rule regardless of the cosmetic label.
    /// </summary>
    private static (ViewTypeGroup group, string display) ClassifyView(View v)
    {
        switch (v.ViewType)
        {
            case ViewType.Section:
                return (ViewTypeGroup.SectionOrCallout, "Section");

            case ViewType.Elevation:
                return (ViewTypeGroup.Elevation, "Elevation");

            case ViewType.FloorPlan:
                return (ViewTypeGroup.FloorPlan, "Floor Plan");

            case ViewType.CeilingPlan:
                return (ViewTypeGroup.CeilingPlan, "Ceiling Plan");

            case ViewType.EngineeringPlan:
                return (ViewTypeGroup.StructuralPlan, "Structural Plan");

            case ViewType.AreaPlan:
                return (ViewTypeGroup.AreaPlan, "Area Plan");

            case ViewType.DraftingView:
                return (ViewTypeGroup.Drafting, "Drafting View");

            case ViewType.Legend:
                return (ViewTypeGroup.Legend, "Legend");

            case ViewType.Schedule:
                return (ViewTypeGroup.Schedule, "Schedule");

            default:
                // InScopeViewTypes already filters the collector to exactly
                // the 9 types handled above, so this should be unreachable.
                // Fail loudly rather than silently mis-bucketing an
                // unexpected view type into an unrelated duplicate-name group.
                throw new System.InvalidOperationException(
                    $"Unhandled ViewType '{v.ViewType}' reached ClassifyView — " +
                    "update InScopeViewTypes/ClassifyView together if a new type was added.");
        }
    }

    /// <summary>
    /// Builds a ViewId → list-of-sheet-numbers lookup for views that can be
    /// placed on multiple sheets (Legends via Viewport, Schedules via
    /// ScheduleSheetInstance). Section/Callout/Elevation/Plan/Drafting views
    /// use the simpler VIEWER_SHEET_NUMBER parameter directly (single place).
    /// </summary>
    private static Dictionary<ElementId, List<string>> BuildPlacedSheetsLookup(Document doc)
    {
        var result = new Dictionary<ElementId, List<string>>();

        void Add(ElementId viewId, string sheetNumber)
        {
            if (viewId == ElementId.InvalidElementId || string.IsNullOrWhiteSpace(sheetNumber)) return;
            if (!result.TryGetValue(viewId, out var list))
            {
                list = new List<string>();
                result[viewId] = list;
            }
            if (!list.Contains(sheetNumber)) list.Add(sheetNumber);
        }

        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .ToList();

        foreach (var sheet in sheets)
        {
            // Legends are placed via Viewport (they behave like any view on a sheet).
            var viewports = new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>();
            foreach (var vp in viewports)
                Add(vp.ViewId, sheet.SheetNumber);

            // Schedules are placed via ScheduleSheetInstance.
            var scheduleInstances = new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(ScheduleSheetInstance))
                .Cast<ScheduleSheetInstance>();
            foreach (var si in scheduleInstances)
                Add(si.ScheduleId, sheet.SheetNumber);
        }

        return result;
    }
}
