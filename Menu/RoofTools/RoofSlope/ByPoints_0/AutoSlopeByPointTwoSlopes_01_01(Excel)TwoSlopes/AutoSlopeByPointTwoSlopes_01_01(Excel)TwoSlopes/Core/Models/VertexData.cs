using Autodesk.Revit.DB;
using System;

namespace AutoSlopeByPointTwoSlopes_01_00.Core.Models
{
    public class VertexData
    {
        public int VertexIndex { get; set; }
        public XYZ Position { get; set; }
        public double PathLengthMeters { get; set; }

        private double _elevationOffsetMm;
        public double ElevationOffsetMm
        {
            get => Math.Round(_elevationOffsetMm, 0);
            set => _elevationOffsetMm = value;
        }

        public int NearestDrainIndex { get; set; }
        public XYZ DirectionVector { get; set; }
        public bool WasProcessed { get; set; }
        public double AppliedSlopePercent { get; set; }
        public bool IsSpecialVertex { get; set; }

        public string Direction =>
            DirectionVector != null ?
            $"{DirectionVector.X:F3},{DirectionVector.Y:F3},{DirectionVector.Z:F3}" :
            "0,0,0";

        public string AppliedSlopeDisplay => $"{AppliedSlopePercent}%";
    }
}