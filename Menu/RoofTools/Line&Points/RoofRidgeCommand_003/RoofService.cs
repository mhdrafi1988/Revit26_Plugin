using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.RRLPV3.Commands;
using Revit22_Plugin.RRLPV3.Models;
using Revit22_Plugin.RRLPV3.Utils;
using Revit22_Plugin.Services;
using System;

namespace Revit22_Plugin.RRLPV3.Services
{
    /// <summary>
    /// The main processing engine for creating ridge detail lines,
    /// perpendiculars, and shape points on roof elements.
    /// Clean, minimal, and Revit API–safe.
    /// </summary>
    public static class RoofService
    {
        /// <summary>
        /// Executes the full roof workflow.
        /// </summary>
        public static RoofData ExecuteRoofProcessing(
            UIDocument uidoc,
            Action<string> updateStatus = null)
        {
            var data = new RoofData();
            Document doc = uidoc.Document;

            try
            {
                data.StartTime = DateTime.Now;

                // -----------------------------
                // Select Roof
                // -----------------------------
                updateStatus?.Invoke("Select roof...");
                data.SelectedRoof = SelectRoof(uidoc);

                if (data.SelectedRoof == null)
                {
                    data.AddLog("No roof selected.");
                    return data;
                }

                data.AddLog($"Selected Roof: {data.SelectedRoof.Id}");

                // -----------------------------
                // Select Points
                // -----------------------------
                updateStatus?.Invoke("Pick two points...");
                if (!PointSelectionService.PickTwoFarPoints(uidoc,
                        out XYZ p1,
                        out XYZ p2))
                {
                    data.AddLog("Point selection failed.");
                    return data;
                }

                data.Point1 = p1;
                data.Point2 = p2;

                data.AddLog($"P1 = ({p1.X:F2}, {p1.Y:F2})");
                data.AddLog($"P2 = ({p2.X:F2}, {p2.Y:F2})");

                // -----------------------------
                // Transaction
                // -----------------------------
                using (Transaction tx = new Transaction(doc, "Roof Ridge Processing"))
                {
                    tx.Start();

                    // -------------------------
                    // Main Detail Line
                    // -------------------------
                    updateStatus?.Invoke("Creating main detail line...");
                    var mainLine = GeometryService.CreateDetailLine(doc, doc.ActiveView, p1, p2);

                    if (mainLine != null)
                    {
                        data.DetailLinesCreated = 1;
                        data.AddLog("Main detail line created.");
                    }

                    // -------------------------
                    // Perpendicular Lines
                    // -------------------------
                    updateStatus?.Invoke("Creating perpendicular lines...");
                    var perps =
                        GeometryService.CreatePerpendicularLines(doc, doc.ActiveView, data.SelectedRoof, p1, p2);

                    data.PerpendicularLinesCreated = perps.Count;
                    data.AddLog($"Perpendiculars created: {perps.Count}");

                    // -------------------------
                    // Shape Points
                    // -------------------------
                    updateStatus?.Invoke("Adding shape points...");
                    data.ShapePointsAdded =
                        GeometryService.AddShapePoints(doc, data.SelectedRoof, perps, data.PointInterval);

                    data.AddLog($"Shape points added: {data.ShapePointsAdded}");

                    tx.Commit();
                }

                // Success
                data.IsSuccess = true;
                data.AddLog("Processing completed successfully.");
            }
            catch (Exception ex)
            {
                data.IsSuccess = false;
                data.AddLog("ERROR: " + ex.Message);
                Logger.LogException(ex, "RoofService.ExecuteRoofProcessing");
            }
            finally
            {
                data.EndTime = DateTime.Now;
                data.AddLog($"Total Time: {data.Duration:mm\\:ss}");
            }

            return data;
        }

        // ----------------------------------------------------
        // Roof Selection
        // ----------------------------------------------------
        public static RoofBase SelectRoof(UIDocument uidoc)
        {
            try
            {
                var r = uidoc.Selection.PickObject(
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a roof element");

                return uidoc.Document.GetElement(r) as RoofBase;
            }
            catch
            {
                return null;
            }
        }
    }
}
