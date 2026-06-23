namespace Revit26_Plugin.OuterCurveDivider.V001.Models
{
    /// <summary>
    /// How a curved edge is divided.
    ///   ByDistance → user spacing in metres; segment count rounded, spacing equalized.
    ///   ByCount    → a fixed number of points (place exactly N points).
    /// </summary>
    public enum DivisionMode
    {
        ByDistance,
        ByCount
    }
}
