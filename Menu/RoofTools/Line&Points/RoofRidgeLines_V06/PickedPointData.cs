// File: PickedPointData.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Models
//
// Responsibility:
// - Stores user-picked point data in an API-agnostic form
// - Used by ViewModels and Execution services

namespace Revit26_Plugin.RoofRidgeLines_V06.Models
{
    /// <summary>
    /// Represents a point picked on the roof surface.
    /// </summary>
    public class PickedPointData
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public PickedPointData(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
