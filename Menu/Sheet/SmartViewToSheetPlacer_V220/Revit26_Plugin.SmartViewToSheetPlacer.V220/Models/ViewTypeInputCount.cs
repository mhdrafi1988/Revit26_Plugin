using Autodesk.Revit.DB;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V220.Models
{
    /// <summary>
    /// One metric card in Stage 2/3's Row 1 ("Selected Views" input summary):
    /// how many selected views belong to one ViewType group. Generic across
    /// any ViewType present in a run (confirmed: not hardcoded to
    /// Plan/Section) — one instance per distinct RevitViewType among
    /// currently selected views, rebuilt every RunPacking().
    /// </summary>
    public class ViewTypeInputCount
    {
        public ViewType RevitViewType { get; }
        public string ViewTypeLabel { get; }
        public int Count { get; }

        public ViewTypeInputCount(ViewType revitViewType, string viewTypeLabel, int count)
        {
            RevitViewType = revitViewType;
            ViewTypeLabel = viewTypeLabel;
            Count = count;
        }
    }
}
