using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.Models;
using Revit26_Plugin.RRLPV8.Constants;
using System;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.Services
{
    public class WizardService
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly View _activeView;
        private readonly IGeometryService _geometry;

        public WizardService(UIDocument uidoc, IGeometryService geometryService = null)
        {
            _uiDoc = uidoc ?? throw new ArgumentNullException(nameof(uidoc));
            _doc = uidoc.Document;
            _activeView = _doc.ActiveView;
            _geometry = geometryService ?? new GeometryService();
        }

        public ExecutionResult ExecuteRoofProcessing()
        {
            var result = new ExecutionResult();
            result.StatusMessage = "Starting roof processing...";

            try
            {
                DateTime startTime = DateTime.Now;

                // 1. Select roof
                result.StatusMessage = "Select roof...";
                var roof = SelectRoof();
                if (roof == null)
                {
                    result.StatusMessage = "No roof selected.";
                    result.IsSuccess = false;
                    return result;
                }

                result.AddLog($"Selected Roof: {roof.Id}");

                // 2. Select points
                result.StatusMessage = "Pick two points...";
                if (!ReferencePicker.PickTwoPoints(_uiDoc, out XYZ p1, out XYZ p2))
                {
                    result.StatusMessage = "Point selection failed.";
                    result.IsSuccess = false;
                    return result;
                }

                result.AddLog($"P1 = ({p1.X:F2}, {p1.Y:F2})");
                result.AddLog($"P2 = ({p2.X:F2}, {p2.Y:F2})");

                // 3. Execute in transaction
                using (Transaction tx = new Transaction(_doc, "Roof Ridge Processing"))
                {
                    tx.Start();

                    // Main detail line
                    result.StatusMessage = "Creating main detail line...";
                    var mainLine = _geometry.CreateDetailLine(_doc, _activeView, p1, p2);
                    if (mainLine != null)
                    {
                        result.DetailLinesCreated = 1;
                        result.AddLog("Main detail line created.");
                    }

                    // Perpendicular lines
                    result.StatusMessage = "Creating perpendicular lines...";
                    var perps = _geometry.CreatePerpendicularLines(_doc, _activeView, roof, p1, p2);
                    result.PerpendicularLinesCreated = perps.Count;
                    result.AddLog($"Perpendiculars created: {perps.Count}");

                    // Shape points
                    result.StatusMessage = "Adding shape points...";
                    result.ShapePointsAdded = _geometry.AddShapePoints(_doc, roof, perps, Constants.DEFAULT_INTERVAL);
                    result.AddLog($"Shape points added: {result.ShapePointsAdded}");

                    tx.Commit();
                }

                result.IsSuccess = true;
                result.StatusMessage = "Processing completed successfully.";
                result.Duration = DateTime.Now - startTime;
                result.AddLog($"Total Time: {result.Duration:mm\\:ss}");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.StatusMessage = $"Error: {ex.Message}";
                result.AddLog($"ERROR: {ex.Message}");
                LoggerService.LogException(ex, "WizardService.ExecuteRoofProcessing");
            }

            return result;
        }

        private RoofBase SelectRoof()
        {
            try
            {
                var reference = _uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a roof element");

                return _doc.GetElement(reference) as RoofBase;
            }
            catch
            {
                return null;
            }
        }
    }

    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}