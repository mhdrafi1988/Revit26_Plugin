using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Revit26_Plugin.DwgToDetailLines.V010.Helpers;
using Revit26_Plugin.DwgToDetailLines.V010.Models;

namespace Revit26_Plugin.DwgToDetailLines.V010.Services
{
    /// <summary>
    /// Converts CAD import geometry into Detail Curves and Filled Regions in
    /// the active Drafting View, scoped to the layers the user checked in the
    /// Layers &amp; Hatches grid. Line layers → DetailCurve (existing V002 path).
    /// Hatch layers → FilledRegion (new in V009), using boundary loops from
    /// CadGeometryExtractor.ExtractHatches. Style/pattern resolution per layer
    /// (Create/Skip prompt, cached) mirrors the same pattern for both paths.
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
            TransformMethod transformMethod,
            int entityCount,
            int layerCount,
            IReadOnlyCollection<string> selectedLineLayers,
            IReadOnlyCollection<string> selectedHatchLayers,
            string defaultLineStyleName,
            string defaultFillPatternName)
        {
            Document doc = _uiApp.ActiveUIDocument.Document;
            View activeView = _uiApp.ActiveUIDocument.ActiveView;
            double tol = doc.Application.ShortCurveTolerance;

            var metrics = new ConversionMetrics
            {
                LayersFound = layerCount,
                Entities = entityCount
            };

            int placed = 0;
            int skipped = 0;
            int failed = 0;

            var lineSet = new HashSet<string>(selectedLineLayers ?? System.Array.Empty<string>());
            var hatchSet = new HashSet<string>(selectedHatchLayers ?? System.Array.Empty<string>());

            var preprocessorResult = TransactionHelper.Run(doc, "DWG to Detail Lines", () =>
            {
                RunLinePass(cad, doc, activeView, spline, transformMethod, tol, lineSet,
                    defaultLineStyleName, ref placed, ref skipped, ref failed);

                RunHatchPass(cad, doc, activeView, transformMethod, hatchSet,
                    defaultFillPatternName, ref placed, ref skipped, ref failed);
            });

            // Elements that were created (counted in `placed` above) but then
            // rejected by Revit's own commit-time geometry validation (e.g.
            // "Line is too short.") were auto-deleted by
            // AutoDeleteFailuresPreprocessor rather than shown as a blocking
            // dialog. Move that count from Placed to Skipped so metrics
            // reflect what's actually in the model.
            if (preprocessorResult.DeletedElementCount > 0)
            {
                placed = System.Math.Max(0, placed - preprocessorResult.DeletedElementCount);
                skipped += preprocessorResult.DeletedElementCount;

                foreach (string msg in preprocessorResult.ResolvedMessages)
                {
                    _log($"[WARN] Element auto-removed at commit: {msg}", Brushes.Orange);
                }
            }

            metrics.Placed = placed;
            metrics.Skipped = skipped;
            metrics.Failed = failed;

            _log($"[SUCCESS] Conversion complete | {placed} placed | {skipped} skipped | {failed} failed",
                Brushes.LightGreen);

            return metrics;
        }

        private void RunLinePass(
            ImportInstance cad,
            Document doc,
            View activeView,
            SplineHandlingMode spline,
            TransformMethod transformMethod,
            double tol,
            HashSet<string> lineSet,
            string defaultLineStyleName,
            ref int placed,
            ref int skipped,
            ref int failed)
        {
            if (lineSet.Count == 0)
                return;

            var curves = CadGeometryExtractor.Extract(cad, doc, activeView, spline, transformMethod, _log, out int extractionSkipped);
            skipped += extractionSkipped;
            var byLayer = curves.Where(c => lineSet.Contains(c.Layer)).GroupBy(c => c.Layer);

            var resolver = new LineStyleResolutionService();
            var styleService = new DetailLineStyleService(doc, resolver);

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

                int layerPlaced = 0;
                int layerFailed = 0;

                foreach (var c in usable)
                {
                    // Pre-filter above catches raw curve.Length < tol, but Revit's
                    // actual endpoint-distance check inside NewDetailCurve can still
                    // reject curves that pass that filter (e.g. arcs, post-transform
                    // geometry). Isolate per-curve so one bad curve doesn't abort the
                    // whole layer/transaction — log it as Failed and move on.
                    try
                    {
                        DetailCurve detailCurve = doc.Create.NewDetailCurve(activeView, c);
                        detailCurve.LineStyle = style;
                        layerPlaced++;
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentException ex)
                    {
                        layerFailed++;
                        _log($"[WARN] Layer '{layerGroup.Key}': curve rejected by Revit ({ex.Message})",
                            Brushes.Orange);
                    }
                }

                placed += layerPlaced;
                failed += layerFailed;

                _log($"[INFO] Detail line style assigned: {layerGroup.Key} ({layerPlaced} lines" +
                     (layerFailed > 0 ? $", {layerFailed} failed)" : ")"),
                    Brushes.LightGray);
            }
        }

        private void RunHatchPass(
            ImportInstance cad,
            Document doc,
            View activeView,
            TransformMethod transformMethod,
            HashSet<string> hatchSet,
            string defaultFillPatternName,
            ref int placed,
            ref int skipped,
            ref int failed)
        {
            if (hatchSet.Count == 0)
                return;

            var hatches = CadGeometryExtractor.ExtractHatches(cad, doc, activeView, transformMethod, _log);
            var byLayer = hatches.Where(h => hatchSet.Contains(h.Layer)).GroupBy(h => h.Layer);

            var resolver = new FillPatternResolutionService();
            var styleService = new DetailFillRegionStyleService(doc, resolver, defaultFillPatternName);

            foreach (var layerGroup in byLayer)
            {
                FilledRegionType type = styleService.GetOrResolve(layerGroup.Key);

                if (type == null)
                {
                    int count = layerGroup.Count();
                    _log($"[WARN] Hatch layer '{layerGroup.Key}' skipped by user choice",
                        Brushes.Orange);
                    skipped += count;
                    continue;
                }

                int layerPlaced = 0;
                int layerFailed = 0;

                foreach (var h in layerGroup)
                {
                    // A degenerate or self-intersecting boundary loop (rare, but
                    // possible from faceted DWG solids) throws inside NewFilledRegion.
                    // Isolate per-hatch so one bad boundary doesn't abort the layer.
                    try
                    {
                        var loops = new List<CurveLoop> { h.Boundary };
                        FilledRegion.Create(doc, type.Id, activeView.Id, loops);
                        layerPlaced++;
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentException ex)
                    {
                        layerFailed++;
                        _log($"[WARN] Hatch layer '{layerGroup.Key}': boundary rejected by Revit ({ex.Message})",
                            Brushes.Orange);
                    }
                }

                placed += layerPlaced;
                failed += layerFailed;

                _log($"[INFO] Fill pattern assigned: {layerGroup.Key} ({layerPlaced} region(s)" +
                     (layerFailed > 0 ? $", {layerFailed} failed)" : ")"),
                    Brushes.LightGray);
            }
        }
    }
}
