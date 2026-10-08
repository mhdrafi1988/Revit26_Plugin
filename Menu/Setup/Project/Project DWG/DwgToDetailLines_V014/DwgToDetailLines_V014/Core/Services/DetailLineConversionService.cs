// ==============================================
// File: DetailLineConversionService.cs
// Layer: Core/Services
// Converts CAD import geometry into Detail Curves and Filled Regions in
// the active Drafting View, scoped to the layers the user checked in the
// Layers &amp; Hatches grid. Line layers -> DetailCurve. Hatch layers ->
// FilledRegion, using boundary loops from CadGeometryExtractor.ExtractHatches.
// Style/pattern resolution per layer (Create/Skip prompt, cached) mirrors
// the same pattern for both paths.
// Changes vs V010: log delegate switched from Action<string, Brush> to the
// shared Action<LogEntry> used across the rest of the suite.
// Changes vs V013:
//   ADDED layerStyleMap (Shortlist mode): each line layer uses the mapped
//         existing style — no prompt, no new styles. Null keeps the V013
//         layer-name behaviour.
//   ADDED hatchTypeMap (hatch Shortlist mode): the same for hatch layers
//         and existing filled region types.
//   ADDED removeDuplicateLines: exact duplicate curves within a layer are
//         skipped (DuplicateCurveFilter).
//   ADDED the created detail lines / filled regions are selected after the
//         conversion commits, so they can be moved or grouped in one go.
//   ADDED offset: every created curve / hatch boundary is translated by it
//         ("Place beside CAD").
//   FIX   a style Revit does not accept for detail curves (e.g. <Room
//         Separation>) fell through to a per-curve exception; it is now
//         checked once and replaced by the default line style.
// ==============================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.DwgToDetailLines.V014.Core.Models;
using Revit26_Plugin.DwgToDetailLines.V014.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Services
{
    /// <summary>
    /// Creates Detail Curves and Filled Regions in the active drafting view from
    /// the checked layers of a CAD import, inside one transaction.
    /// </summary>
    public class DetailLineConversionService
    {
        private readonly UIApplication _uiApp;
        private readonly System.Action<LogEntry> _log;
        private readonly List<ElementId> _created = new();

        /// <summary>Creates the service; <paramref name="log"/> receives progress lines.</summary>
        public DetailLineConversionService(UIApplication uiApp, System.Action<LogEntry> log)
        {
            _uiApp = uiApp;
            _log = log;
        }

        /// <summary>
        /// Converts the selected layers and returns the resulting metrics.
        /// </summary>
        /// <param name="layerStyleMap">
        /// Shortlist mode: CAD layer → existing line style name. Null = Layer Name mode
        /// (style named like the layer, created or skipped through a prompt).
        /// </param>
        /// <param name="hatchTypeMap">
        /// Hatch Shortlist mode: CAD hatch layer → existing filled region type name.
        /// Null = Layer Name mode (type named like the layer, created or skipped through a prompt).
        /// </param>
        /// <param name="removeDuplicateLines">Skip exact duplicate curves within a layer.</param>
        /// <param name="offset">Translation applied to all created geometry, or null for none.</param>
        public ConversionMetrics Execute(
            ImportInstance cad,
            SplineHandlingMode spline,
            TransformMethod transformMethod,
            int entityCount,
            int layerCount,
            IReadOnlyCollection<string> selectedLineLayers,
            IReadOnlyCollection<string> selectedHatchLayers,
            string defaultLineStyleName,
            string defaultFillPatternName,
            IReadOnlyDictionary<string, string> layerStyleMap,
            IReadOnlyDictionary<string, string> hatchTypeMap,
            bool removeDuplicateLines,
            XYZ offset)
        {
            Document doc = _uiApp.ActiveUIDocument.Document;
            View activeView = _uiApp.ActiveUIDocument.ActiveView;
            _created.Clear();
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

            Transform shift = offset != null && !offset.IsZeroLength()
                ? Transform.CreateTranslation(offset)
                : null;

            var preprocessorResult = TransactionHelper.Run(doc, "DWG to Detail Lines", () =>
            {
                RunLinePass(cad, doc, activeView, spline, transformMethod, tol, lineSet,
                    defaultLineStyleName, layerStyleMap, removeDuplicateLines, shift, ref placed, ref skipped, ref failed);

                RunHatchPass(cad, doc, activeView, transformMethod, hatchSet,
                    defaultFillPatternName, hatchTypeMap, shift, ref placed, ref skipped, ref failed);
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
                    _log(new LogEntry(LogLevel.Warning, $"Element auto-removed at commit: {msg}"));
                }
            }

            SelectCreated(doc);

            metrics.Placed = placed;
            metrics.Skipped = skipped;
            metrics.Failed = failed;

            _log(new LogEntry(LogLevel.Success,
                $"Conversion complete | {placed} placed | {skipped} skipped | {failed} failed"));

            return metrics;
        }

        /// <summary>
        /// Selects the elements created by this run that survived the commit
        /// (commit-time failures auto-delete some). Selection is a convenience:
        /// a failure here is logged, never thrown.
        /// </summary>
        private void SelectCreated(Document doc)
        {
            var ids = _created.Where(id => doc.GetElement(id) != null).ToList();
            if (ids.Count == 0)
                return;

            try
            {
                _uiApp.ActiveUIDocument.Selection.SetElementIds(ids);
                _log(new LogEntry(LogLevel.Info, $"Selected {ids.Count} new element(s)"));
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException ex)
            {
                _log(new LogEntry(LogLevel.Warning, $"Could not select the new elements: {ex.Message}"));
            }
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
            IReadOnlyDictionary<string, string> layerStyleMap,
            bool removeDuplicateLines,
            Transform shift,
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
            GraphicsStyle defaultStyle = styleService.GetByName(defaultLineStyleName);

            // Styles Revit accepts on a detail curve — read once from the first
            // curve created, then reused for every layer.
            HashSet<ElementId> validStyleIds = null;
            double vertexTol = doc.Application.VertexTolerance;
            int duplicatesTotal = 0;

            foreach (var layerGroup in byLayer)
            {
                int shortCount = 0;
                var usable = new List<Curve>();

                foreach (var c in layerGroup)
                {
                    Curve curve = c.Curve;
                    if (shift != null)
                    {
                        try
                        {
                            curve = curve.CreateTransformed(shift);
                        }
                        catch (Autodesk.Revit.Exceptions.ArgumentException)
                        {
                            shortCount++;
                            continue;
                        }
                    }

                    if (curve.Length < tol)
                    {
                        shortCount++;
                        continue;
                    }
                    usable.Add(curve);
                }

                skipped += shortCount;

                if (shortCount > 0)
                {
                    _log(new LogEntry(LogLevel.Info, $"Layer '{layerGroup.Key}': short curves skipped = {shortCount}"));
                }

                if (removeDuplicateLines)
                {
                    usable = DuplicateCurveFilter.RemoveDuplicates(usable, vertexTol, out int duplicates);
                    if (duplicates > 0)
                    {
                        skipped += duplicates;
                        duplicatesTotal += duplicates;
                        _log(new LogEntry(LogLevel.Info, $"Layer '{layerGroup.Key}': duplicate lines skipped = {duplicates}"));
                    }
                }

                GraphicsStyle style;
                if (layerStyleMap == null)
                {
                    style = styleService.GetOrResolve(layerGroup.Key);

                    if (style == null)
                    {
                        _log(new LogEntry(LogLevel.Warning, $"Layer '{layerGroup.Key}' skipped by user choice"));
                        skipped += usable.Count;
                        continue;
                    }
                }
                else
                {
                    layerStyleMap.TryGetValue(layerGroup.Key, out string mapped);
                    style = styleService.GetByName(mapped);

                    if (style == null)
                    {
                        style = defaultStyle;
                        _log(new LogEntry(LogLevel.Warning,
                            $"Layer '{layerGroup.Key}': mapped line style '{mapped ?? "(none)"}' not found — " +
                            (style != null ? $"using default '{defaultLineStyleName}'" : "Revit default style kept")));
                    }
                }

                bool styleChecked = false;

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
                        _created.Add(detailCurve.Id);

                        if (!styleChecked)
                        {
                            validStyleIds ??= new HashSet<ElementId>(detailCurve.GetLineStyleIds());
                            styleChecked = true;

                            if (style != null && !validStyleIds.Contains(style.Id))
                            {
                                GraphicsStyle fallback = defaultStyle != null && validStyleIds.Contains(defaultStyle.Id)
                                    ? defaultStyle
                                    : null;
                                _log(new LogEntry(LogLevel.Warning,
                                    $"Layer '{layerGroup.Key}': line style '{style.Name}' cannot be used on detail lines — " +
                                    (fallback != null ? $"using default '{fallback.Name}'" : "Revit default style kept")));
                                style = fallback;
                            }
                        }

                        if (style != null)
                            detailCurve.LineStyle = style;
                        layerPlaced++;
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentException ex)
                    {
                        layerFailed++;
                        _log(new LogEntry(LogLevel.Warning, $"Layer '{layerGroup.Key}': curve rejected by Revit ({ex.Message})"));
                    }
                }

                placed += layerPlaced;
                failed += layerFailed;

                _log(new LogEntry(LogLevel.Info,
                    $"Detail line style assigned: {layerGroup.Key} → {style?.Name ?? "(Revit default)"} ({layerPlaced} lines" +
                    (layerFailed > 0 ? $", {layerFailed} failed)" : ")")));
            }

            if (duplicatesTotal > 0)
                _log(new LogEntry(LogLevel.Info, $"Duplicate lines skipped in total: {duplicatesTotal}"));
        }

        private void RunHatchPass(
            ImportInstance cad,
            Document doc,
            View activeView,
            TransformMethod transformMethod,
            HashSet<string> hatchSet,
            string defaultFillPatternName,
            IReadOnlyDictionary<string, string> hatchTypeMap,
            Transform shift,
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
                FilledRegionType type;
                if (hatchTypeMap == null)
                {
                    type = styleService.GetOrResolve(layerGroup.Key);

                    if (type == null)
                    {
                        int count = layerGroup.Count();
                        _log(new LogEntry(LogLevel.Warning, $"Hatch layer '{layerGroup.Key}' skipped by user choice"));
                        skipped += count;
                        continue;
                    }
                }
                else
                {
                    hatchTypeMap.TryGetValue(layerGroup.Key, out string mapped);
                    type = styleService.GetByName(mapped);

                    if (type == null)
                    {
                        type = styleService.GetByName(defaultFillPatternName);
                        _log(new LogEntry(LogLevel.Warning,
                            $"Hatch layer '{layerGroup.Key}': mapped fill type '{mapped ?? "(none)"}' not found — " +
                            (type != null ? $"using default '{defaultFillPatternName}'" : "layer skipped")));

                        if (type == null)
                        {
                            skipped += layerGroup.Count();
                            continue;
                        }
                    }
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
                        CurveLoop boundary = shift != null
                            ? CurveLoop.CreateViaTransform(h.Boundary, shift)
                            : h.Boundary;
                        var loops = new List<CurveLoop> { boundary };
                        _created.Add(FilledRegion.Create(doc, type.Id, activeView.Id, loops).Id);
                        layerPlaced++;
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentException ex)
                    {
                        layerFailed++;
                        _log(new LogEntry(LogLevel.Warning, $"Hatch layer '{layerGroup.Key}': boundary rejected by Revit ({ex.Message})"));
                    }
                }

                placed += layerPlaced;
                failed += layerFailed;

                _log(new LogEntry(LogLevel.Info,
                    $"Fill pattern assigned: {layerGroup.Key} → {type.Name} ({layerPlaced} region(s)" +
                    (layerFailed > 0 ? $", {layerFailed} failed)" : ")")));
            }
        }
    }
}
