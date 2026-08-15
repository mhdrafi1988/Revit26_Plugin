using Autodesk.Revit.DB;

namespace Revit26_Plugin.BubbleAutoRenumber.V006.Models
{
    internal class SectionEntry
    {
        public Viewport    Vp     { get; init; }
        public ViewSection View   { get; init; }
        public XYZ         Center { get; init; }
        public Outline     Box    { get; init; }
    }
}
