using Autodesk.Revit.DB;

namespace Revit26_Plugin.CalloutCOP_V04.Models
{
    public class ViewFilterState
    {
        public ViewSheet SelectedSheet { get; set; }
        public bool ShowPlaced { get; set; } = true;
        public bool ShowUnplaced { get; set; } = true;
        public string SearchText { get; set; } = string.Empty;
    }
}
