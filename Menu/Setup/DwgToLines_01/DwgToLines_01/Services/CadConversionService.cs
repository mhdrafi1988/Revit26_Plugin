using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Revit26_Plugin.DwgSymbolicConverter_V01.Models;
using Revit26_Plugin.DwgSymbolicConverter_V01.Services;

// ? ALIAS TO AVOID WPF CONFLICT
using PlacementModeModel =
    Revit26_Plugin.DwgSymbolicConverter_V01.Models.PlacementMode;

namespace Revit26_Plugin.DwgSymbolicConverter_V01.Services
{
    public class CadConversionService
    {
        private readonly UIApplication _uiApp;
        private readonly Action<string, Brush> _log;

        public CadConversionService(
            UIApplication uiApp,
            Action<string, Brush> log)
        {
            _uiApp = uiApp;
            _log = log;
        }

        public void Execute(
            SplineHandlingMode splineMode,
            PlacementModeModel placementMode)
        {
            UIDocument uidoc = _uiApp.ActiveUIDocument;
            Document doc = uidoc.Document;

            ImportInstance cad =
                uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<ImportInstance>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException("No CAD Import or Link selected.");

            ViewPlan refLevel = FindRefLevel(doc);

            if (refLevel == null && placementMode != PlacementModeModel.ModelOnly)
                throw new InvalidOperationException("Ref. Level view not found.");

            var curves =
                CadGeometryExtractor.Extract(
                    cad,
                    doc,
                    splineMode,
                    _log);

            var styleService = new SymbolicLineStyleService(doc);

            // ================= SYMBOLIC LINES =================
            if (placementMode != PlacementModeModel.ModelOnly)
            {
                uidoc.ActiveView = refLevel;

                SketchPlane sketchPlane =
                    refLevel.SketchPlane ??
                    SketchPlane.Create(doc, refLevel.GenLevel.Id);

                using (Transaction t = new Transaction(doc, "Create Symbolic Lines"))
                {
                    t.Start();

                    foreach (var (curve, layer) in curves)
                    {
                        var sc = doc.FamilyCreate.NewSymbolicCurve(curve, sketchPlane);
                        sc.LineStyle = styleService.GetOrCreate(layer);
                    }

                    t.Commit();
                }

                _log("[INFO] Symbolic lines placed in Ref. Level",
                    Brushes.LightGreen);
            }

            // ================= MODEL LINES =================
            if (placementMode != PlacementModeModel.SymbolicOnly)
            {
                SketchPlane sketchPlane =
                    SketchPlane.Create(
                        doc,
                        Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero));

                using (Transaction t = new Transaction(doc, "Create Model Lines"))
                {
                    t.Start();

                    foreach (var (curve, _) in curves)
                    {
                        doc.FamilyCreate.NewModelCurve(curve, sketchPlane);
                    }

                    t.Commit();
                }

                _log("[INFO] Model lines placed (family-wide)",
                    Brushes.LightBlue);
            }

            _log("[SUCCESS] Geometry creation complete",
                Brushes.LightGreen);
        }

        private ViewPlan FindRefLevel(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .FirstOrDefault(v =>
                    v.ViewType == ViewType.FloorPlan &&
                    v.Name == "Ref. Level");
        }
    }
}
