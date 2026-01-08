using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Models;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Utils;
using System;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Services
{
    public static class RoofService
    {
        public static RoofData Execute(UIDocument uidoc, Action<string> status)
        {
            var data = new RoofData { StartTime = DateTime.Now };
            Document doc = uidoc.Document;

            try
            {
                status?.Invoke("Select roof");
                data.SelectedRoof = RoofSelectionService.SelectRoof(uidoc);
                if (data.SelectedRoof == null) return data;

                status?.Invoke("Pick points");
                XYZ point1, point2;
                if (!PointSelectionService.PickTwoFarPoints(uidoc, out point1, out point2))
                    return data;
                data.Point1 = point1;
                data.Point2 = point2;

                using (Transaction tx = new Transaction(doc, "Roof Ridge Lines"))
                {
                    tx.Start();

                    GeometryService.CreateDetailLine(doc, doc.ActiveView, data.Point1, data.Point2);
                    var perps = GeometryService.CreatePerpendicularLines(
                        doc, doc.ActiveView, data.SelectedRoof, data.Point1, data.Point2);

                    data.ShapePointsAdded =
                        GeometryService.AddShapePoints(doc, data.SelectedRoof, perps, data.PointInterval);

                    tx.Commit();
                }

                data.IsSuccess = true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "RoofService");
            }
            finally
            {
                data.EndTime = DateTime.Now;
            }

            return data;
        }
    }
}
