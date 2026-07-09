using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Revit26_Plugin.DwgSymbolicConverter_V03.Helpers;
using Revit26_Plugin.DwgSymbolicConverter_V03.Models;
using DBTransform = Autodesk.Revit.DB.Transform;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Services
{
    public class CadConversionService
    {
        private readonly UIApplication _uiApp;
        private readonly System.Action<string, Brush> _log;

        public CadConversionService(UIApplication uiApp, System.Action<string, Brush> log)
        {
            _uiApp = uiApp;
            _log = log;
        }

        public void Execute(
            ImportInstance cad,
            PlacementMode placement,
            SplineHandlingMode spline,
            bool extrusionReady)
        {
            Document doc = _uiApp.ActiveUIDocument.Document;
            double tol = doc.Application.ShortCurveTolerance;

            var curves = CadGeometryExtractor.Extract(
                cad, doc, doc.ActiveView, spline, _log);

            var byLayer = curves.GroupBy(c => c.Layer);

            TransactionHelper.Run(doc, "DWG Convert", () =>
            {
                foreach (var layerGroup in byLayer)
                {
                    int shortCount = 0;

                    var usable = new List<Curve>();

                    foreach (var c in layerGroup)
                    {
                        if (c.Curve.Length < tol)
                        {
                            shortCount++;
                            continue;
                        }
                        usable.Add(c.Curve);
                    }

                    if (extrusionReady)
                    {
                        usable = CurveCleanupHelper.SnapEndpoints(usable, tol);

                        if (!CurveCleanupHelper.FormsClosedLoop(usable, tol))
                        {
                            _log($"[WARN] Layer '{layerGroup.Key}' not closed",
                                Brushes.Orange);
                        }
                    }

                    _log($"[INFO] Layer '{layerGroup.Key}': short curves skipped = {shortCount}",
                        Brushes.Goldenrod);

                    foreach (var c in usable)
                    {
                        if (placement != PlacementMode.ModelOnly)
                            doc.FamilyCreate.NewSymbolicCurve(c,
                                SketchPlane.Create(doc,
                                    Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero)));

                        if (placement != PlacementMode.SymbolicOnly)
                        {
                            Curve flat = c.CreateTransformed(
                                DBTransform.Identity);

                            doc.FamilyCreate.NewModelCurve(flat,
                                SketchPlane.Create(doc,
                                    Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero)));
                        }
                    }
                }
            });

            _log("[SUCCESS] Conversion complete", Brushes.LightGreen);
        }
    }
}
