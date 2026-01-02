using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_adv_V001.Models
{
    /// <summary>
    /// Graph node representing a roof shape vertex.
    /// Equality is ID-based (CRITICAL for A* / Dijkstra).
    /// </summary>
    public class GraphNode
    {
        public int Id { get; }
        public XYZ Point { get; }
        public double Z => Point.Z;

        public bool IsCorner { get; set; }
        public bool IsDrain { get; set; }

        public IList<GraphNode> Neighbors { get; } = new List<GraphNode>();

        public GraphNode(int id, XYZ point)
        {
            Id = id;
            Point = point;
        }

        public double DistanceTo(GraphNode other)
        {
            return Point.DistanceTo(other.Point);
        }

        public double Distance2DTo(GraphNode other)
        {
            XYZ a = new XYZ(Point.X, Point.Y, 0);
            XYZ b = new XYZ(other.Point.X, other.Point.Y, 0);
            return a.DistanceTo(b);
        }

        // -----------------------------
        // 🔴 CRITICAL: Equality override
        // -----------------------------
        public override bool Equals(object obj)
        {
            return obj is GraphNode other && other.Id == Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override string ToString()
        {
            return $"Node {Id} (Z={Z:0.###})";
        }
    }
}
