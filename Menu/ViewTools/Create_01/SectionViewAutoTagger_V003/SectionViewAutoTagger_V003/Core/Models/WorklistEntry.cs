using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionViewAutoTagger.V003
{
    /// <summary>
    /// One row queued in the Worklist card: a sheet, the section view(s) on
    /// it that were checked, and the categories (with their locked-in tag
    /// type) selected to tag. Immutable once added — per Rafi's
    /// confirmation, worklist rows are delete-only (no in-place edit); to
    /// change one, remove it and re-add.
    ///
    /// V003: Categories changed from List&lt;BuiltInCategory&gt; to
    /// List&lt;CategoryTagSelection&gt; — tag type is now resolved once at
    /// "Add to Worklist" time (from CategoryTagRow.SelectedTagType) and
    /// travels with the entry; it is NOT re-resolved at Run time.
    /// </summary>
    public class WorklistEntry
    {
        public ElementId SheetId { get; }
        public string SheetNumber { get; }
        public string SheetName { get; }

        /// <summary>The section view(s) on this sheet that were checked when this entry was added.</summary>
        public IReadOnlyList<SectionViewOption> Views { get; }

        /// <summary>The categories (with locked-in tag type) that were checked and taggable when this entry was added.</summary>
        public IReadOnlyList<CategoryTagSelection> Categories { get; }

        /// <summary>Display line 1: "{SheetNumber} — {view names joined}".</summary>
        public string SheetDisplay => $"{SheetNumber} — {string.Join(", ", Views.Select(v => v.ViewName))}";

        /// <summary>Display line 2: "CategoryName (TagTypeName)" joined, e.g. "Doors (M_Door Tag: Standard), Windows (M_Window Tag: Standard)".</summary>
        public string CategoriesDisplay { get; }

        public WorklistEntry(
            ElementId sheetId,
            string sheetNumber,
            string sheetName,
            IReadOnlyList<SectionViewOption> views,
            IReadOnlyList<CategoryTagSelection> categories,
            string categoriesDisplay)
        {
            SheetId = sheetId;
            SheetNumber = sheetNumber;
            SheetName = sheetName;
            Views = views;
            Categories = categories;
            CategoriesDisplay = categoriesDisplay;
        }
    }
}
