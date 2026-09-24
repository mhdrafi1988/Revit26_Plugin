using Autodesk.Revit.DB;

namespace Revit26_Plugin.WorksetsElementsBrowser.WSEB002.Core.Models
{
    /// <summary>One selectable 3D view, offered in the "Show in 3D View" picker.</summary>
    public class View3DOption
    {
        public ElementId ViewId { get; }
        public string Name { get; }

        public View3DOption(ElementId viewId, string name)
        {
            ViewId = viewId;
            Name = name;
        }
    }
}
