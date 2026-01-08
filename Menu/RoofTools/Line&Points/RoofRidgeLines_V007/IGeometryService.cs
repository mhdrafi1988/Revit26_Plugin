using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.Services
{
    public interface IGeometryService
    {
        DetailLine CreateDetailLine(Document doc, View view, XYZ p1, XYZ p2);
        List<DetailLine> CreatePerpendicularLines(Document doc, View view, RoofBase roof, XYZ p1, XYZ p2);
        int AddShapePoints(Document doc, RoofBase roof, List<DetailLine> perpendicularLines, double interval);
        List<Curve> GetRoofBoundaryCurves(RoofBase roof);
        PlanarFace GetTopFace(RoofBase roof);
    }
}