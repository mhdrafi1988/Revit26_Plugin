using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;
using System.Windows.Media;
using Revit26_Plugin.DwgSymbolicConverter_V02.Models;

using PlacementModeModel =
    Revit26_Plugin.DwgSymbolicConverter_V02.Models.PlacementMode;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Services
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
            ImportInstance cad,
            SplineHandlingMode splineMode,
            PlacementModeModel placementMode)
        {
            UIDocument uidoc = _uiApp.ActiveUIDocument;
            Document doc = uidoc.Document;

            _log("[DEBUG] Execute() entered", Brushes.Cyan);
            _log($"[INFO] Using CAD import: {cad.Name}", Brushes.White);

            ViewPlan refLevel = FindRefLevel(doc)
                ?? throw new InvalidOperationException("Ref. Level not found");

            SketchPlane sketchPlane = EnsureSketchPlane(doc, refLevel);

            var curves = CadGeometryExtractor.Extract(
                cad,
                doc,
                refLevel,
                sketchPlane,
                splineMode,
                _log);

            if (curves.Count == 0)
            {
                _log("[WARN] No geometry extracted", Brushes.Goldenrod);
                return;
            }

            var styleService = new SymbolicLineStyleService(doc);

            if (placementMode != PlacementModeModel.ModelOnly)
            {
                using (Transaction t =
                    new Transaction(doc, "Symbolic Lines"))
                {
                    t.Start();

                    foreach (var item in curves)
                    {
                        var sc =
                            doc.FamilyCreate.NewSymbolicCurve(
                                item.curve,
                                sketchPlane);

                        sc.LineStyle =
                            styleService.GetOrCreate(item.layer);
                    }

                    t.Commit();
                }

                _log("[INFO] Symbolic lines created", Brushes.LightGreen);
            }

            if (placementMode != PlacementModeModel.SymbolicOnly)
            {
                using (Transaction t =
                    new Transaction(doc, "Model Lines"))
                {
                    t.Start();

                    foreach (var item in curves)
                        doc.FamilyCreate.NewModelCurve(
                            item.curve,
                            sketchPlane);

                    t.Commit();
                }

                _log("[INFO] Model lines created", Brushes.LightBlue);
            }

            _log("[SUCCESS] Conversion complete", Brushes.LightGreen);
        }

        private SketchPlane EnsureSketchPlane(
            Document doc,
            ViewPlan view)
        {
            if (view.SketchPlane != null)
                return view.SketchPlane;

            using (Transaction t =
                new Transaction(doc, "Create SketchPlane"))
            {
                t.Start();

                var sp = SketchPlane.Create(
                    doc,
                    Plane.CreateByNormalAndOrigin(
                        XYZ.BasisZ, XYZ.Zero));

                view.SketchPlane = sp;

                t.Commit();
                return sp;
            }
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
