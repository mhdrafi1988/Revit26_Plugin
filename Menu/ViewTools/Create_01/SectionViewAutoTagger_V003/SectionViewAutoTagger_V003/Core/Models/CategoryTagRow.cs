using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SectionViewAutoTagger.V003
{
    /// <summary>
    /// One row in the "Categories in Selected View(s)" checklist. Represents
    /// a BuiltInCategory found present in the currently-checked section
    /// view(s). IsTaggable reflects whether at least one loaded tag family
    /// resolves for this category (see TagFamilyResolverService); untaggable
    /// rows are shown disabled with a Reason, never silently hidden.
    ///
    /// ASSUMPTION: categories are scanned across the UNION of all currently
    /// checked views (one shared checklist), matching the mockup's single
    /// "Categories in Selected View(s)" card. If per-view category sets are
    /// wanted instead, this model and the scan/worklist flow need rework.
    ///
    /// V003: added AvailableTagTypes/SelectedTagType/HasMultipleTagTypes for
    /// per-category tag type selection. Confirmed UI behavior: when only one
    /// tag type is loaded, the grid shows plain text (no dropdown) — but
    /// SelectedTagType is still populated so Add to Worklist has a value to
    /// lock in either way. Uses TagTypeOption (not raw FamilySymbol) so XAML
    /// has a clean DisplayName to bind to.
    /// </summary>
    public partial class CategoryTagRow : ObservableObject
    {
        /// <summary>The model category this row represents.</summary>
        public BuiltInCategory Category { get; }

        /// <summary>Display name, e.g. "Doors".</summary>
        public string CategoryName { get; }

        /// <summary>True if at least one loaded tag family/type was resolved for this category.</summary>
        public bool IsTaggable { get; }

        /// <summary>
        /// Reason shown in the status badge when not taggable, e.g.
        /// "No Tag Family Loaded". Empty when IsTaggable is true.
        /// </summary>
        public string Reason { get; }

        /// <summary>All loaded tag type options resolved for this category. Empty when IsTaggable is false.</summary>
        public IReadOnlyList<TagTypeOption> AvailableTagTypes { get; }

        /// <summary>
        /// True when 2+ tag types are available — drives the grid's
        /// text-vs-dropdown switch (dropdown only shown when there's an
        /// actual choice to make).
        /// </summary>
        public bool HasMultipleTagTypes => AvailableTagTypes.Count > 1;

        [ObservableProperty]
        private bool isSelected;

        /// <summary>
        /// The tag type to use for this category. Defaults to
        /// AvailableTagTypes[0] when taggable. User-changeable via the
        /// dropdown only when HasMultipleTagTypes; otherwise this is the
        /// single resolved option shown as read-only text.
        /// </summary>
        [ObservableProperty]
        private TagTypeOption selectedTagType;

        public CategoryTagRow(
            BuiltInCategory category,
            string categoryName,
            bool isTaggable,
            IReadOnlyList<FamilySymbol> availableTagTypeSymbols,
            string reason = "")
        {
            Category = category;
            CategoryName = categoryName;
            IsTaggable = isTaggable;
            AvailableTagTypes = (availableTagTypeSymbols ?? new List<FamilySymbol>())
                .Select(fs => new TagTypeOption(fs))
                .ToList();
            Reason = reason;
            selectedTagType = AvailableTagTypes.Count > 0 ? AvailableTagTypes[0] : null;
        }
    }
}
