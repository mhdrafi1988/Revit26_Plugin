using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser.Models
{
    public class ProcessedLine
    {
        public XYZ Start { get; }
        public XYZ End { get; }

        public double StartZ { get; }
        public double EndZ { get; }

        public ProcessedLine(XYZ start, XYZ end, double startZ, double endZ)
        {
            Start = start ?? throw new ArgumentNullException(nameof(start));
            End = end ?? throw new ArgumentNullException(nameof(end));

            StartZ = startZ;
            EndZ = endZ;
        }
    }
}
