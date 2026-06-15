using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using Revit26_Plugin.DwgSymbolicConverter_V02.Models;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Services
{
    /// <summary>
    /// Read-only scan phase: summarizes extracted CAD geometry for UI display + logging.
    /// </summary>
    public class CadGeometryScanService
    {
        private readonly UIApplication _uiApp;
        private readonly Action<string, Brush> _log;

        public CadGeometryScanService(UIApplication uiApp, Action<string, Brush> log)
        {
            _uiApp = uiApp;
            _log = log;
        }

        /// <summary>
        /// Scans the given ImportInstance using the same extractor pipeline (transform + projection),
        /// then produces summary rows for the DataGrid.
        /// </summary>
        public void Scan(
            ImportInstance cadInstance,
            View targetView,
            SketchPlane targetSketchPlane,
            SplineHandlingMode splineMode,
            ObservableCollection<CadGeometrySummary> output)
        {
            output.Clear();

            _log("[INFO] Geometry scan started", Brushes.White);

            var extracted = CadGeometryExtractor.Extract(
                cadInstance,
                _uiApp.ActiveUIDocument.Document,
                targetView,
                targetSketchPlane,
                splineMode,
                _log);

            if (extracted.Count == 0)
            {
                _log("[WARN] Geometry scan found 0 curves", Brushes.Goldenrod);
                return;
            }

            // Type breakdown
            var byType = extracted
                .GroupBy(x => GetCurveTypeName(x.curve))
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            // Layer breakdown
            var byLayer = extracted
                .GroupBy(x => x.layer ?? "DWG-Default")
                .Select(g => new { Layer = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            _log($"[INFO] Total extracted curves: {extracted.Count}", Brushes.White);

            foreach (var t in byType)
                _log($"[INFO] {t.Type}: {t.Count}", Brushes.White);

            _log($"[INFO] Layers found: {byLayer.Count}", Brushes.White);

            // Populate grid rows as "type + layer" (layer-wise entity count requirement)
            var typeLayer = extracted
                .GroupBy(x => new { Type = GetCurveTypeName(x.curve), Layer = x.layer ?? "DWG-Default" })
                .Select(g => new CadGeometrySummary
                {
                    GeometryType = g.Key.Type,
                    LayerName = g.Key.Layer,
                    Count = g.Count()
                })
                .OrderByDescending(r => r.Count)
                .ThenBy(r => r.LayerName)
                .ThenBy(r => r.GeometryType);

            foreach (var row in typeLayer)
                output.Add(row);

            _log("[INFO] Geometry scan complete", Brushes.White);
        }

        private static string GetCurveTypeName(Curve c)
        {
            if (c is Line) return "Line";
            if (c is Arc) return "Arc";
            if (c is NurbSpline) return "Spline";
            return "Curve";
        }
    }
}
