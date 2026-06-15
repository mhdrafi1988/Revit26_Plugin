using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutoLiner_V01.Models
{
    public class FlowStartPoint
    {
        public XYZ Position { get; }
        public string SourceType { get; } // "Ridge" | "Corner"

        public FlowStartPoint(XYZ position, string sourceType)
        {
            Position = position;
            SourceType = sourceType;
        }
    }
}
