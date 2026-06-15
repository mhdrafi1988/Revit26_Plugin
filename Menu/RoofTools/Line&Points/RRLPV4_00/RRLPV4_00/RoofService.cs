using Autodesk.Revit.DB;
using System;
using Revit26_Plugin.RRLPV4.Models;

namespace Revit26_Plugin.RRLPV4.Services
{
    public static class RoofService
    {
        public static void ExecuteRoofProcessing(
            RoofData roofData,
            Action<string> log)
        {
            if (roofData?.SelectedRoof == null)
                throw new ArgumentNullException(nameof(roofData));

            Document doc = roofData.SelectedRoof.Document;
            View view = doc.ActiveView;

            int divs = roofData.DivisionStrategy switch
            {
                "Divide by 2" => 2,
                "Divide by 5" => 5,
                _ => 3
            };

            using Transaction t = new(doc, "Roof Ridge Lines");
            t.Start();

            try
            {
                // Create main ridge line
                var mainLine = GeometryDetailService.CreateDetailLine(
                    doc, view,
                    roofData.Point1,
                    roofData.Point2,
                    roofData.UsedLineStyle);

                roofData.DetailLinesCreated++;

                // Create perpendicular lines
                var perpLines = GeometryDetailService.CreatePerpendicularLines(
                    doc, view,
                    roofData.Point1,
                    roofData.Point2,
                    divs,
                    roofData.UsedLineStyle);

                roofData.PerpendicularLinesCreated = perpLines.Count;

                t.Commit();

                log($"Created {roofData.DetailLinesCreated} main line and " +
                    $"{roofData.PerpendicularLinesCreated} perpendicular lines.");
            }
            catch (Exception)
            {
                t.RollBack();
                throw;
            }
        }
    }
}