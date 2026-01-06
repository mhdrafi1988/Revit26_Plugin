using Autodesk.Revit.DB;

namespace Revit26_Plugin.CreaserAdv_V002.Models
{
    public class FlattenedEdge2D
    {
        public XYZ Start2D { get; }
        public XYZ End2D { get; }
        public bool IsCrease { get; }

        public FlattenedEdge2D(XYZ start2D, XYZ end2D, bool isCrease)
        {
            Start2D = start2D;
            End2D = end2D;
            IsCrease = isCrease;
        }

        public Line ToLine2D()
        {
            return Line.CreateBound(Start2D, End2D);
        }
    }
}
