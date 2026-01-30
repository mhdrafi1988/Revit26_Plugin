// ==============================================
// File: DwgItemModel.cs
// Layer: ViewModels
// ==============================================

using Autodesk.Revit.DB;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.ViewModels
{
    /// <summary>
    /// Lightweight UI model for DWG selection.
    /// </summary>
    public class DwgItemModel
    {
        public ElementId ElementId { get; }
        public string DisplayName { get; }

        public DwgItemModel(ElementId id, string displayName)
        {
            ElementId = id;
            DisplayName = displayName;
        }

        public override string ToString()
            => DisplayName;
    }
}
