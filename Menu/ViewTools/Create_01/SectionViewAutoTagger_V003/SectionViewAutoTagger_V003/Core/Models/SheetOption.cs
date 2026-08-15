using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionViewAutoTagger.V003
{
    /// <summary>
    /// One entry in the Sheet dropdown. Wraps a ViewSheet's number/name for
    /// display and carries the ElementId for scanning its placed section views.
    /// </summary>
    public class SheetOption
    {
        /// <summary>ViewSheet.SheetNumber, e.g. "A-201".</summary>
        public string SheetNumber { get; }

        /// <summary>ViewSheet.Name, e.g. "Level 1 Sections".</summary>
        public string SheetName { get; }

        /// <summary>ElementId of the underlying ViewSheet.</summary>
        public ElementId SheetId { get; }

        /// <summary>Display text for the dropdown: "{Number} — {Name}".</summary>
        public string DisplayName => $"{SheetNumber} — {SheetName}";

        public SheetOption(string sheetNumber, string sheetName, ElementId sheetId)
        {
            SheetNumber = sheetNumber;
            SheetName = sheetName;
            SheetId = sheetId;
        }

        public override string ToString() => DisplayName;
    }
}
