// File: SectionFootprint.cs
namespace Revit26_Plugin.APUS_V318.Models
{
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
            return $"{WidthFt:F2} × {HeightFt:F2} ft";
        }
    }
}