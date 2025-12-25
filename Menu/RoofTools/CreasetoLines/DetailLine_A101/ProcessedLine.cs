using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_A101.Models
{
    /// <summary>
    /// Immutable data model representing a processed crease line.
    /// </summary>
    internal sealed class ProcessedLine
    {
        public XYZ Start { get; }
        public XYZ End { get; }

        public ProcessedLine(XYZ start, XYZ end)
        {
            Start = start;
            End = end;
        }

        public double Length => Start.DistanceTo(End);
    }
}
