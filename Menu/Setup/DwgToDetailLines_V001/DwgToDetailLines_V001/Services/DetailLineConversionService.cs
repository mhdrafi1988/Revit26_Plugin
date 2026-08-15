using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Revit26_Plugin.DwgToDetailLines.V001.Helpers;
using Revit26_Plugin.DwgToDetailLines.V001.Models;

namespace Revit26_Plugin.DwgToDetailLines.V001.Services
{
    /// <summary>
    /// Converts CAD import geometry into Detail Curves in the active Drafting View.
    /// Assigns resolved line styles per DWG layer (Create/Skip prompt, cached per layer).
    /// </summary>
    public class DetailLineConversionService
    {
        private readonly UIApplication _uiApp;
        private readonly System.Action<string, Brush> _log;

        public DetailLineConversionService(UIApplication uiApp, System.Action<string, Brush> log)
        {
            _uiApp = uiApp;
            _log = log;
        }

        public ConversionMetrics Execute(
            ImportInstance cad,
            SplineHandlingMode spline,
            int entityCount,
            int layerCount)
        {
            Document doc = _uiApp.ActiveUIDocument.Document;
            View activeView = _uiApp.ActiveUIDocument.ActiveView;
            double tol = doc.Application.ShortCurveTolerance;

            var metrics = new ConversionMetrics
            {
                LayersFound = layerCount,
                Entities = entityCount
            };

            var curves = CadGeometryExtractor.Extract(
                cad, doc, activeView, spline, _log);

            var byLayer = curves.GroupBy(c => c.Layer);

            var resolver = new LineStyleResolutionService();
            var styleService = new DetailLineStyleService(doc, resolver);

            int placed = 0;
            int skipped = 0;

            TransactionHelper.Run(doc, "DWG to Detail Lines", () =>
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

                    skipped += shortCount;

                    if (shortCount > 0)
                    {
                        _log($"[INFO] Layer '{layerGroup.Key}': short curves skipped = {shortCount}",
                            Brushes.Goldenrod);
                    }

                    GraphicsStyle style = styleService.GetOrResolve(layerGroup.Key);

                    if (style == null)
                    {
                        _log($"[WARN] Layer '{layerGroup.Key}' skipped by user choice",
                            Brushes.Orange);
                        skipped += usable.Count;
                        continue;
                    }

                    foreach (var c in usable)
                    {
                        DetailCurve detailCurve = doc.Create.NewDetailCurve(activeView, c);

                        detailCurve.LineStyle = style;
                        placed++;
                    }

                    _log($"[INFO] Detail line style assigned: {layerGroup.Key} ({usable.Count} lines)",
                        Brushes.LightGray);
                }
            });

            metrics.Placed = placed;
            metrics.Skipped = skipped;

            _log($"[SUCCESS] Conversion complete | {placed} placed | {skipped} skipped | 0 failed",
                Brushes.LightGreen);

            return metrics;
        }
    }
}
