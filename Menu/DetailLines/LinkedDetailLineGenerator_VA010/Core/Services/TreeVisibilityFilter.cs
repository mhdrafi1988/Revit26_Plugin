using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Revit26_Plugin.LinkedDetailLineGenerator.VA010.Core.Models;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA010.Core.Services
{
    /// <summary>
    /// Builds type-group flat views and applies search/metric filters
    /// to the element tree — extracted from MainViewModel (VA010 Option 3).
    /// </summary>
    internal static class TreeVisibilityFilter
    {
        internal readonly record struct Filters(
            string SearchText,
            double? WidthMinFt, double? WidthMaxFt,
            double? HeightMinFt, double? HeightMaxFt,
            double? PerimeterMinFt, double? PerimeterMaxFt,
            double? AreaMinSqFt, double? AreaMaxSqFt);

        /// <summary>Populates the three flat TypeGroup collections from the same
        /// TypeTreeItem instances the Category tree uses.</summary>
        internal static void BuildTypeGroups(
            List<LinkTreeNode> nodes,
            ObservableCollection<TypeGroupItem> profile,
            ObservableCollection<TypeGroupItem> linear,
            ObservableCollection<TypeGroupItem> point)
        {
            var allTypes = nodes
                .SelectMany(n => n.Categories)
                .SelectMany(c => c.Families)
                .SelectMany(f => f.Types)
                .ToList();

            FillGroup(profile, allTypes, RepresentationGroup.Profile);
            FillGroup(linear, allTypes, RepresentationGroup.Linear);
            FillGroup(point, allTypes, RepresentationGroup.Point);
        }

        private static void FillGroup(
            ObservableCollection<TypeGroupItem> target,
            List<TypeTreeItem> allTypes,
            RepresentationGroup group)
        {
            foreach (var g in allTypes
                .Where(t => t.Group == group)
                .GroupBy(t => t.TypeName)
                .OrderBy(g => g.Key))
            {
                var item = new TypeGroupItem { TypeName = g.Key, Group = group };
                foreach (var t in g) item.Members.Add(t);
                target.Add(item);
            }
        }

        /// <summary>Re-derives IsVisible on every tree node and type-group item
        /// using the current search text and metric filters.</summary>
        internal static void Apply(
            ObservableCollection<LinkTreeNode> tree,
            ObservableCollection<TypeGroupItem> profile,
            ObservableCollection<TypeGroupItem> linear,
            ObservableCollection<TypeGroupItem> point,
            Filters f)
        {
            foreach (var node in tree)
                foreach (var cat in node.Categories)
                {
                    bool anyCatVisible = false;
                    foreach (var fam in cat.Families)
                    {
                        bool anyFamVisible = false;
                        foreach (var t in fam.Types)
                        {
                            t.IsVisible = Matches(t, f);
                            anyFamVisible |= t.IsVisible;
                        }
                        fam.IsVisible = anyFamVisible;
                        anyCatVisible |= anyFamVisible;
                    }
                    cat.IsVisible = anyCatVisible;
                }

            foreach (var group in profile.Concat(linear).Concat(point))
                group.IsVisible = group.Members.Any(m => m.IsVisible);

            foreach (var node in tree)
                node.RefreshVisibilityFlags();
        }

        private static bool Matches(TypeTreeItem t, Filters f)
        {
            if (!string.IsNullOrWhiteSpace(f.SearchText))
            {
                bool hit = t.TypeName.Contains(f.SearchText, System.StringComparison.OrdinalIgnoreCase)
                    || t.FamilyName.Contains(f.SearchText, System.StringComparison.OrdinalIgnoreCase)
                    || t.CategoryName.Contains(f.SearchText, System.StringComparison.OrdinalIgnoreCase);
                if (!hit) return false;
            }

            var m = t.Metrics;
            if (m == null) return true;

            if (f.WidthMinFt.HasValue && m.WidthFeet < f.WidthMinFt.Value) return false;
            if (f.WidthMaxFt.HasValue && m.WidthFeet > f.WidthMaxFt.Value) return false;
            if (f.HeightMinFt.HasValue && m.HeightFeet < f.HeightMinFt.Value) return false;
            if (f.HeightMaxFt.HasValue && m.HeightFeet > f.HeightMaxFt.Value) return false;
            if (f.PerimeterMinFt.HasValue && m.PerimeterFeet.HasValue && m.PerimeterFeet.Value < f.PerimeterMinFt.Value) return false;
            if (f.PerimeterMaxFt.HasValue && m.PerimeterFeet.HasValue && m.PerimeterFeet.Value > f.PerimeterMaxFt.Value) return false;
            if (f.AreaMinSqFt.HasValue && m.AreaSqFt.HasValue && m.AreaSqFt.Value < f.AreaMinSqFt.Value) return false;
            if (f.AreaMaxSqFt.HasValue && m.AreaSqFt.HasValue && m.AreaSqFt.Value > f.AreaMaxSqFt.Value) return false;

            return true;
        }
    }
}
