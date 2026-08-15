using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionViewAutoTagger.V003
{
    /// <summary>
    /// One category's tag-type choice, locked in at "Add to Worklist" time
    /// and carried unchanged into the WorklistEntry. Confirmed: the tag
    /// type is resolved once when added to the worklist (from
    /// CategoryTagRow.SelectedTagType) and is NOT re-resolved at Run time —
    /// if tag families change between queuing and running, the batch still
    /// uses the FamilySymbol that was selected when queued.
    /// </summary>
    public class CategoryTagSelection
    {
        public BuiltInCategory Category { get; }

        /// <summary>Display name, e.g. "Doors".</summary>
        public string CategoryName { get; }

        /// <summary>The FamilySymbol.Id locked in when this row was added to the worklist.</summary>
        public ElementId TagTypeId { get; }

        /// <summary>Display name of the locked-in tag type, e.g. "M_Door Tag : Standard".</summary>
        public string TagTypeName { get; }

        public CategoryTagSelection(BuiltInCategory category, string categoryName, ElementId tagTypeId, string tagTypeName)
        {
            Category = category;
            CategoryName = categoryName;
            TagTypeId = tagTypeId;
            TagTypeName = tagTypeName;
        }
    }
}
