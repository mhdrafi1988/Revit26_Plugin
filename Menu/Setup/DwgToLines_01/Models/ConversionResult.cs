namespace Revit26_Plugin.DwgSymbolicConverter_V01.Models
{
    public class ConversionResult
    {
        public int TotalDetected { get; set; }
        public int Created { get; set; }
        public int Skipped { get; set; }
        public int TessellatedSegments { get; set; }
    }

    public enum SplineHandlingMode
    {
        Preserve,
        Tessellate
    }
}
