using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionViewAutoTagger.V004
{
    /// <summary>
    /// Display wrapper around a loaded tag FamilySymbol, used by
    /// CategoryTagRow.AvailableTagTypes/SelectedTagType. XAML binds to
    /// DisplayName rather than the raw FamilySymbol, since FamilySymbol has
    /// no "Family : Type" display property of its own (its Name is just the
    /// type name, e.g. "Standard" — not useful alone when multiple families
    /// share a type name).
    /// </summary>
    public class TagTypeOption
    {
        /// <summary>The underlying FamilySymbol — its Id is what gets locked into CategoryTagSelection.TagTypeId.</summary>
        public FamilySymbol Symbol { get; }

        /// <summary>"{Family Name} : {Type Name}", e.g. "M_Door Tag : Standard".</summary>
        public string DisplayName { get; }

        public TagTypeOption(FamilySymbol symbol)
        {
            Symbol = symbol;
            string familyName = symbol.Family?.Name ?? "";
            DisplayName = string.IsNullOrEmpty(familyName) ? symbol.Name : $"{familyName} : {symbol.Name}";
        }
    }
}
