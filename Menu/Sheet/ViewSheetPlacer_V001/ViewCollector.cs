using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Tools.ViewSheetPlacer
{
    /// <summary>Result of a document scan: rows plus the union of parameter names.</summary>
    public sealed class ViewScan
    {
        public List<ViewInfo> Views { get; } = new();
        public List<string> ParameterNames { get; } = new();
        public List<TitleblockOption> Titleblocks { get; } = new();
    }

    public sealed class TitleblockOption
    {
        public ElementId TypeId { get; init; } = ElementId.InvalidElementId;
        public string Display { get; init; } = string.Empty;
        public string UniqueId { get; init; } = string.Empty;
    }

    /// <summary>Read-only pass over the document. No transaction needed.</summary>
    public static class ViewCollector
    {
        private static readonly HashSet<ViewType> Allowed = new()
        {
            ViewType.FloorPlan, ViewType.CeilingPlan, ViewType.Section,
            ViewType.Elevation, ViewType.ThreeD, ViewType.DraftingView,
            ViewType.Legend, ViewType.Detail, ViewType.AreaPlan,
            ViewType.EngineeringPlan
        };

        public static ViewScan Scan(Document doc)
        {
            var scan = new ViewScan();

            // Map placed views -> (sheet number, viewport id).
            var placement = new Dictionary<ElementId, (string Sheet, ElementId Vp)>();
            var viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport)).Cast<Viewport>();
            foreach (var vp in viewports)
            {
                if (doc.GetElement(vp.SheetId) is ViewSheet sheet)
                    placement[vp.ViewId] = (sheet.SheetNumber, vp.Id);
            }

            var paramNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View)).Cast<View>()
                .Where(v => !v.IsTemplate && Allowed.Contains(v.ViewType));

            foreach (var v in views)
            {
                bool isPlaced = placement.TryGetValue(v.Id, out var p);

                var paramValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Parameter param in v.Parameters)
                {
                    if (param?.Definition == null) continue;
                    string name = param.Definition.Name;
                    string val = SafeValueString(param);
                    paramNames.Add(name);
                    if (!paramValues.ContainsKey(name))
                        paramValues[name] = val;
                }

                scan.Views.Add(new ViewInfo
                {
                    ViewId = v.Id,
                    ViewName = v.Name,
                    ViewType = FriendlyType(v.ViewType),
                    Scale = ScaleString(v),
                    Discipline = DisciplineString(v),
                    IsPlaced = isPlaced,
                    PlacedSheet = isPlaced ? p.Sheet : string.Empty,
                    ExistingViewportId = isPlaced ? p.Vp : ElementId.InvalidElementId,
                    ParamValues = paramValues
                });
            }

            scan.Views.Sort((a, b) =>
                string.Compare(a.ViewName, b.ViewName, StringComparison.OrdinalIgnoreCase));
            scan.ParameterNames.AddRange(paramNames);

            // Titleblock types.
            var tbs = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType().Cast<ElementType>();
            foreach (var tb in tbs)
            {
                scan.Titleblocks.Add(new TitleblockOption
                {
                    TypeId = tb.Id,
                    UniqueId = tb.UniqueId,
                    Display = $"{tb.FamilyName} : {tb.Name}"
                });
            }
            scan.Titleblocks.Sort((a, b) =>
                string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase));

            return scan;
        }

        private static string SafeValueString(Parameter p)
        {
            try
            {
                if (p.StorageType == StorageType.String) return p.AsString() ?? string.Empty;
                string vs = p.AsValueString();
                return vs ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string DisciplineString(View v)
        {
            var p = v.get_Parameter(BuiltInParameter.VIEW_DISCIPLINE);
            return p != null ? (p.AsValueString() ?? string.Empty) : string.Empty;
        }

        private static string ScaleString(View v)
        {
            try
            {
                if (v.ViewType == ViewType.ThreeD || v.ViewType == ViewType.Legend)
                    return "—";
                int s = v.Scale;
                return s > 0 ? $"1:{s}" : "—";
            }
            catch { return "—"; }
        }

        private static string FriendlyType(ViewType t) => t switch
        {
            ViewType.FloorPlan => "Floor Plan",
            ViewType.CeilingPlan => "Ceiling Plan",
            ViewType.ThreeD => "3D View",
            ViewType.DraftingView => "Drafting",
            ViewType.AreaPlan => "Area Plan",
            ViewType.EngineeringPlan => "Structural Plan",
            _ => t.ToString()
        };
    }
}
