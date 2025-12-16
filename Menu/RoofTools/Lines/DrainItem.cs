using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutoLiner_V01.Models
{
    public class DrainItem
    {
        public XYZ CenterPoint { get; }

        // ✅ Backward-compatible alias
        public XYZ Center => CenterPoint;

        public DrainItem(XYZ centerPoint)
        {
            CenterPoint = centerPoint;
        }
    }
}
