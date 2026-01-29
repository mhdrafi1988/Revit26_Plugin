using Autodesk.Revit.DB;

namespace Revit26_Plugin.CSFL_V07.Models
{
    /// <summary>
    /// Immutable result describing section orientation.
    /// </summary>
    public class OrientationResult
    {
        public XYZ XDir { get; init; }
        public XYZ YDir { get; init; }
        public XYZ ZDir { get; init; }
        public XYZ MidPoint { get; init; }
        public bool Success { get; init; }
    }
}
