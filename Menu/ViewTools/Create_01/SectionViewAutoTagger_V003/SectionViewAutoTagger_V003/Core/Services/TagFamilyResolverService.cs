using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionViewAutoTagger.V003
{
    /// <summary>
    /// Resolves whether a model BuiltInCategory has loaded tag family/type(s)
    /// available, and returns the FamilySymbol(s) to choose from.
    ///
    /// ASSUMPTION: Revit's tag-category mapping is used (e.g. OST_Doors tags
    /// use family category OST_DoorTags). Categories with no corresponding
    /// Revit tag category at all (e.g. OST_StructuralFraming has no native
    /// tag-by-category support in some template setups) are distinguished
    /// from categories that DO have a tag category but no loaded instance —
    /// this affects the Reason text shown in CategoryTagRow.
    ///
    /// PERF: results are cached per (Document, BuiltInCategory) for the
    /// lifetime of this instance. A fresh FilteredElementCollector over all
    /// FamilySymbols was previously re-run on every call — for a batch Run
    /// touching many views x many categories this repeated the same full
    /// document scan redundantly. Caller should reuse one instance for the
    /// duration of a scan/batch (see SectionViewAutoTaggerEngine's single
    /// _tagResolver field) rather than creating a new one per call.
    ///
    /// V003: added ResolveAllTagTypes, which returns every loaded
    /// FamilySymbol for a category (not just the first) — needed to
    /// populate the per-category Tag Type dropdown and to detect the
    /// single-vs-multiple case that decides plain text vs. dropdown in the
    /// UI. ResolveTagType (single-result) is retained for the engine's
    /// Run-time path where only IsTaggable/first-match matters.
    /// </summary>
    public class TagFamilyResolverService
    {
        private readonly Dictionary<BuiltInCategory, (FamilySymbol TagType, string Reason)> _cache = new();
        private readonly Dictionary<BuiltInCategory, (List<FamilySymbol> TagTypes, string Reason)> _allCache = new();

        /// <summary>
        /// Attempts to resolve a loaded tag FamilySymbol for the given model category.
        /// Returns null if none found; reason is always populated on failure.
        /// Cached per category — call ClearCache() if tag families may have
        /// been loaded/removed mid-session (not needed within a single batch Run).
        /// </summary>
        public FamilySymbol ResolveTagType(Document doc, BuiltInCategory modelCategory, out string reason)
        {
            if (_cache.TryGetValue(modelCategory, out var cached))
            {
                reason = cached.Reason;
                return cached.TagType;
            }

            var all = ResolveAllTagTypes(doc, modelCategory, out reason);
            var first = all.Count > 0 ? all[0] : null;
            _cache[modelCategory] = (first, reason);
            return first;
        }

        /// <summary>
        /// Returns every loaded tag FamilySymbol for the given model
        /// category (empty list if none). Reason is populated only when the
        /// list is empty. Cached per category, same lifetime/invalidation
        /// rules as ResolveTagType.
        /// </summary>
        public List<FamilySymbol> ResolveAllTagTypes(Document doc, BuiltInCategory modelCategory, out string reason)
        {
            if (_allCache.TryGetValue(modelCategory, out var cached))
            {
                reason = cached.Reason;
                return cached.TagTypes;
            }

            reason = string.Empty;

            BuiltInCategory? tagCategory = MapToTagCategory(modelCategory);
            if (tagCategory == null)
            {
                reason = "No Tag Category in Revit";
                _allCache[modelCategory] = (new List<FamilySymbol>(), reason);
                return new List<FamilySymbol>();
            }

            var tagTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => fs.Category != null && fs.Category.Id.Value == (long)tagCategory.Value)
                .OrderBy(fs => fs.Family?.Name)
                .ThenBy(fs => fs.Name)
                .ToList();

            if (tagTypes.Count == 0)
            {
                reason = "No Tag Family Loaded";
            }

            _allCache[modelCategory] = (tagTypes, reason);
            return tagTypes;
        }

        /// <summary>Clears both resolution caches. Call if the document's loaded families may have changed since the last resolve (e.g. between separate scan and Run invocations).</summary>
        public void ClearCache()
        {
            _cache.Clear();
            _allCache.Clear();
        }

        /// <summary>
        /// Model category → Revit tag category mapping. Extend this table as
        /// new categories are confirmed needed; unmapped categories return null.
        /// </summary>
        private BuiltInCategory? MapToTagCategory(BuiltInCategory modelCategory)
        {
            switch (modelCategory)
            {
                case BuiltInCategory.OST_Doors: return BuiltInCategory.OST_DoorTags;
                case BuiltInCategory.OST_Windows: return BuiltInCategory.OST_WindowTags;
                case BuiltInCategory.OST_Walls: return BuiltInCategory.OST_WallTags;
                case BuiltInCategory.OST_Rooms: return BuiltInCategory.OST_RoomTags;
                case BuiltInCategory.OST_Furniture: return BuiltInCategory.OST_FurnitureTags;
                case BuiltInCategory.OST_PlumbingFixtures: return BuiltInCategory.OST_PlumbingFixtureTags;
                case BuiltInCategory.OST_MechanicalEquipment: return BuiltInCategory.OST_MechanicalEquipmentTags;
                case BuiltInCategory.OST_ElectricalEquipment: return BuiltInCategory.OST_ElectricalEquipmentTags;
                case BuiltInCategory.OST_StructuralColumns: return BuiltInCategory.OST_StructuralColumnTags;
                case BuiltInCategory.OST_StructuralFraming: return BuiltInCategory.OST_StructuralFramingTags;
                case BuiltInCategory.OST_Floors: return BuiltInCategory.OST_FloorTags;
                case BuiltInCategory.OST_Ceilings: return BuiltInCategory.OST_CeilingTags;
                case BuiltInCategory.OST_Roofs: return BuiltInCategory.OST_RoofTags;
                case BuiltInCategory.OST_Columns: return BuiltInCategory.OST_ColumnTags;
                default: return null;
            }
        }
    }
}
