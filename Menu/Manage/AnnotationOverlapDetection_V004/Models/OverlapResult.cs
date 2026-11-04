namespace Revit26_Plugin.AnnotationOverlapDetection.V004.Models
{
    /// <summary>
    /// One overlapping pair, as displayed in the results DataGrid.
    /// Columns are kept in their original order; columns added in v4.1.0 are appended at the end.
    /// </summary>
    public class OverlapResult
    {
        /// <summary>Id of the first element of the pair.</summary>
        public long ElementId1 { get; set; }

        /// <summary>Id of the second element of the pair.</summary>
        public long ElementId2 { get; set; }

        /// <summary>Annotation type, or "TypeA / TypeB" for a cross-type pair.</summary>
        public string AnnotationType { get; set; }

        /// <summary>Min corner X of element 1, model mm in view coordinates (4 decimals in UI).</summary>
        public double X { get; set; }

        /// <summary>Min corner Y of element 1, model mm in view coordinates (4 decimals in UI).</summary>
        public double Y { get; set; }

        /// <summary>"Yes" when X or Y of the two elements match within tolerance.</summary>
        public string GridAligned { get; set; }

        /// <summary>Vertical gap in model mm (2 decimals).</summary>
        public double VerticalDistance { get; set; }

        /// <summary>Horizontal gap in model mm (2 decimals).</summary>
        public double HorizontalDistance { get; set; }

        /// <summary>Width of the overlap area as printed on the sheet, paper mm (v4.1.0).</summary>
        public double OverlapWidthPaper { get; set; }

        /// <summary>Height of the overlap area as printed on the sheet, paper mm (v4.1.0).</summary>
        public double OverlapHeightPaper { get; set; }
    }
}
