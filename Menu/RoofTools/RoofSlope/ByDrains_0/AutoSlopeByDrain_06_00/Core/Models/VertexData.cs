using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutoSlopeByDrain_06_00.Core.Models
{
    public class VertexData
    {
        public int VertexIndex { get; set; }
        public XYZ Position { get; set; }
        public double PathLengthMeters { get; set; }
        public double ElevationOffsetMm { get; set; }
        public int NearestDrainIndex { get; set; }
        public XYZ DirectionVector { get; set; }
        public bool WasProcessed { get; set; }

        public string Direction =>
            DirectionVector != null ?
            $"{DirectionVector.X:F3},{DirectionVector.Y:F3},{DirectionVector.Z:F3}" :
            "0,0,0";
    }
}