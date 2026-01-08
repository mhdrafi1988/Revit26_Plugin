using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Services;
using System;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V10.Services
{
    public class WizardService
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly Action<string> _statusCallback;

        public WizardService(
            UIDocument uidoc,
            Action<string> statusCallback)
        {
            _uiDoc = uidoc ?? throw new ArgumentNullException(nameof(uidoc));
            _doc = uidoc.Document;
            _statusCallback = statusCallback;
        }

        public void Execute()
        {
            // FIX A — enforce Plan View
            if (!(_doc.ActiveView is ViewPlan))
                throw new InvalidOperationException(
                    "Roof Ridge Lines can only be executed from a Plan View.");

            // Roof selection (FIX B)
            _statusCallback?.Invoke("Select a roof...");
            RoofBase roof;
            try
            {
                roof = PickRoof();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                _statusCallback?.Invoke("Roof selection cancelled.");
                return;
            }

            if (roof == null)
            {
                _statusCallback?.Invoke("Invalid roof selected.");
                return;
            }

            // Point selection (FIX B)
            XYZ p1, p2;
            try
            {
                _statusCallback?.Invoke("Pick first point...");
                p1 = _uiDoc.Selection.PickPoint("Pick first point");

                _statusCallback?.Invoke("Pick second point...");
                p2 = _uiDoc.Selection.PickPoint("Pick second point");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                _statusCallback?.Invoke("Point selection cancelled.");
                return;
            }

            // Transaction
            using (Transaction tx = new Transaction(_doc, "Create Roof Ridge Line"))
            {
                tx.Start();

                Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, p1);
                SketchPlane sketchPlane = SketchPlane.Create(_doc, plane);

                _doc.Create.NewModelCurve(
                    Line.CreateBound(p1, p2),
                    sketchPlane);

                tx.Commit();
            }

            _statusCallback?.Invoke("Roof ridge line created successfully.");
        }

        private RoofBase PickRoof()
        {
            Reference reference = _uiDoc.Selection.PickObject(
                ObjectType.Element,
                new RoofSelectionFilter(),
                "Select a roof element");

            return _doc.GetElement(reference) as RoofBase;
        }
    }
}
