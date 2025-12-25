using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V100.Models
{
    public class GraphNode
    {
        public int Id { get; }
        public XYZ Point { get; }

        public GraphNode(int id, XYZ point)
        {
            Id = id;
            Point = point;
        }
    }
}
