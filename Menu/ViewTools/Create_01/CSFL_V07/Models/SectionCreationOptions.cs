using Autodesk.Revit.DB;
using Revit26_Plugin.CSFL_V07.Enums;

namespace Revit26_Plugin.CSFL_V07.Models
{
    /// <summary>
    /// Immutable snapshot of user-defined creation settings.
    /// UI-independent.
    /// </summary>
    public class SectionCreationOptions
    {
        public string Prefix { get; }
        public double FarClipMm { get; }
        public double TopPaddingMm { get; }
        public double BottomPaddingMm { get; }
        public double BottomOffsetMm { get; }
        public SnapSourceMode SnapSource { get; }
        public ViewFamilyType SectionType { get; }
        public View Template { get; }

        public bool OpenAfterCreate { get; }
        public bool DeleteLinesAfterCreate { get; }

        public SectionCreationOptions(
            string prefix,
            double farClipMm,
            double topPaddingMm,
            double bottomPaddingMm,
            double bottomOffsetMm,
            SnapSourceMode snapSource,
            ViewFamilyType sectionType,
            View template,
            bool openAfterCreate,
            bool deleteLinesAfterCreate)
        {
            Prefix = prefix;
            FarClipMm = farClipMm;
            TopPaddingMm = topPaddingMm;
            BottomPaddingMm = bottomPaddingMm;
            BottomOffsetMm = bottomOffsetMm;
            SnapSource = snapSource;
            SectionType = sectionType;
            Template = template;
            OpenAfterCreate = openAfterCreate;
            DeleteLinesAfterCreate = deleteLinesAfterCreate;
        }
    }
}
