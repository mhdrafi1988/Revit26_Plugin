namespace Revit26_Plugin.AnnotationOverlapDetection.V002.Models
{
    /// <summary>
    /// One overlapping pair, as displayed in the results DataGrid.
    /// </summary>
    public class OverlapResult
    {
        public long ElementId1 { get; set; }
        public long ElementId2 { get; set; }
        public string AnnotationType { get; set; }

        public double X { get; set; }   // mm, 4 decimals in UI
        public double Y { get; set; }   // mm, 4 decimals in UI

        public string GridAligned { get; set; } // "Yes" / "No"

        public double VerticalDistance { get; set; }   // mm, 2 decimals
        public double HorizontalDistance { get; set; } // mm, 2 decimals
    }
}
