namespace Revit26_Plugin.APUS_V306.Models
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
    }
}
