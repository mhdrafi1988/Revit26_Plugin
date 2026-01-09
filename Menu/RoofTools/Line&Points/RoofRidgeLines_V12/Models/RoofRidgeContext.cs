using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Models
{
    public class RoofRidgeContext
    {
        public UIDocument UiDocument { get; }
        public Document Document { get; }
        public View View { get; }
        public RoofBase Roof { get; }
        public XYZ StartPoint { get; }
        public XYZ EndPoint { get; }
        public double PointIntervalMeters { get; }

        public RoofRidgeContext(
            UIDocument uiDoc,
            RoofBase roof,
            XYZ start,
            XYZ end,
            double intervalMeters)
        {
            UiDocument = uiDoc;
            Document = uiDoc.Document;
            View = uiDoc.Document.ActiveView;
            Roof = roof;
            StartPoint = start;
            EndPoint = end;
            PointIntervalMeters = intervalMeters;
        }
    }
}
