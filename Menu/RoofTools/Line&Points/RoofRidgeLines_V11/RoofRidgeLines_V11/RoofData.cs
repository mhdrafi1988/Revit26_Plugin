using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Models
{
    public class RoofData
    {
        public RoofBase SelectedRoof { get; set; }
        public XYZ Point1 { get; set; }
        public XYZ Point2 { get; set; }
        public double PointInterval { get; set; } = 1.0;
        public bool IsSuccess { get; set; }
        public int ShapePointsAdded { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
    }
}
