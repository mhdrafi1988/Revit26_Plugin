using Autodesk.Revit.DB;

namespace Revit22_Plugin.V4_02.Domain.Models
{
    public class DrainItem
    {
        // ===============================
        // GEOMETRY (MODEL DATA)
        // ===============================
        public XYZ CenterPoint { get; }
        public double Width { get; }   // mm
        public double Height { get; }  // mm

        // ===============================
        // CLASSIFICATION
        // ===============================
        public string ShapeType { get; }
        public string SizeCategory { get; }

        // ===============================
        // REVIT LINK (OPTIONAL)
        // ===============================
        public ElementId SourceElementId { get; }

        // ===============================
        // UI STATE
        // ===============================
        public bool IsSelected { get; set; }

        // ===============================
        // CTOR
        // ===============================
        public DrainItem(
            XYZ center,
            double widthMm,
            double heightMm,
            string shape,
            ElementId sourceId = null)
        {
            CenterPoint = center;
            Width = widthMm;
            Height = heightMm;
            ShapeType = shape;
            SourceElementId = sourceId;

            SizeCategory = ClassifySize(widthMm, heightMm);
            IsSelected = true; // default ON
        }

        // ===============================
        // HELPERS
        // ===============================
        private string ClassifySize(double w, double h)
        {
            double area = w * h;

            if (area < 10000) return "Small";
            if (area < 40000) return "Medium";
            return "Large";
        }

        public override string ToString()
        {
            return $"{SizeCategory} {ShapeType} ({Width:0}x{Height:0} mm)";
        }
    }
}
