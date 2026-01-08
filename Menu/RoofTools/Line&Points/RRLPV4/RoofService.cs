using Autodesk.Revit.DB;
using System;
using Revit26_Plugin.RRLPV4.Models;
using Revit26_Plugin.RRLPV4.Services;

namespace Revit26_Plugin.RRLPV4.Services
{
    public static class RoofService
    {
        public static void ExecuteRoofProcessing(
            Autodesk.Revit.DB.RoofBase roof,
            GraphicsStyle lineStyle,
            string division,
            Action<string> log)
        {
            Document doc = roof.Document;
            View view = doc.ActiveView;

            int divs = division switch
            {
                "Divide by 2" => 2,
                "Divide by 5" => 5,
                _ => 3
            };

            XYZ p1 = new XYZ(0, 0, 0);
            XYZ p2 = new XYZ(30, 0, 0);

            using Transaction t = new(doc, "Roof Ridge Lines");
            t.Start();

            GeometryService.CreateDetailLine(doc, view, p1, p2, lineStyle);
            GeometryService.CreatePerpendicularLines(doc, view, p1, p2, divs, lineStyle);

            t.Commit();

            log("Roof processing completed.");
        }
    }
}
