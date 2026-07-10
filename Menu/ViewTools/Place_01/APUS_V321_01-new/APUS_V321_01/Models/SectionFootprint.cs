// File: SectionFootprint.cs
namespace Revit26_Plugin.APUS_V321_01.Models
{
    /// <summary>
    /// Real rendered viewport footprint, always sourced from
    /// Viewport.GetBoxOutline() (see ViewportMeasurementService). V320 mixed
    /// this with a separate crop/scale-based size source for utilization
    /// reporting, which made the two numbers disagree; V321 uses this as the
    /// single size source everywhere.
    /// </summary>
    public class SectionFootprint
    {
        public double WidthFt { get; }
        public double HeightFt { get; }

        public SectionFootprint(double widthFt, double heightFt)
        {
            WidthFt = widthFt;
            HeightFt = heightFt;
        }

        public override string ToString()
        {
            return $"{WidthFt:F2} x {HeightFt:F2} ft";
        }
    }
}
