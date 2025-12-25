namespace Revit26_Plugin.Creaser_V100.Models
{
    public class GraphEdge
    {
        public int From { get; }
        public int To { get; }
        public double Weight { get; }

        public GraphEdge(int from, int to, double weight)
        {
            From = from;
            To = to;
            Weight = weight;
        }
    }
}
