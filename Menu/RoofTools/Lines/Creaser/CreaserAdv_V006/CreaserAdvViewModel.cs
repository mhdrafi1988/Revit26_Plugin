// ==================================
// File: CreaserAdvViewModel.cs
// Namespace: Revit26_Plugin.CreaserAdv_V006_00
// ==================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.CreaserAdv_V006_00.Services;
using Revit26_Plugin.Shared.Models;          // LogEntry, LogLevel, converters
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;

namespace Revit26_Plugin.CreaserAdv_V006_00.ViewModels
{
    /// <summary>
    /// ViewModel for Creaser Advanced V006_00.
    ///
    /// Pipeline:
    ///   1. Extract crease curves  (top-face / top-face solid edges)
    ///   1b. Filter out horizontal creases (same Z on both endpoints) — always runs
    ///   2. Optionally extract boundary curves  (top-face / side-face edges)
    ///   2b. (Optional, ticked by default) Filter by Dijkstra path validity —
    ///       creases + boundary together; keeps only segments that are a valid
    ///       first hop toward a minimum-elevation drain node
    ///   3. Project all curves to plan-view Z elevation (curve+line pairs kept
    ///      index-aligned — a dropped zero-length projection removes both)
    ///   4. (Optional) Filter creases by minimum length (curve+line pairs, index-aligned)
    ///   5. (Optional) Group creases by drain proximity, remove longest per start point
    ///   6. Place detail items along filtered lines
    ///   7. Populate <see cref="Summary"/> for the summary bar
    /// </summary>
    public partial class CreaserAdvViewModel : ObservableObject
    {
        // --------------------------------------------------
        // Fields
        // --------------------------------------------------

        private readonly UIDocument     _uiDoc;
        private readonly Document       _doc;
        private readonly Element        _roof;
        private readonly LoggingService _log;

        // --------------------------------------------------
        // UI-bound collections
        // --------------------------------------------------

        public ObservableCollection<FamilySymbol> DetailSymbols { get; }
            = new ObservableCollection<FamilySymbol>();

        /// <summary>Bound to the log ListBox. Entries come from LoggingService.</summary>
        public ObservableCollection<LogEntry> LogEntries => _log.Entries;

        // --------------------------------------------------
        // Observable properties
        // --------------------------------------------------

        [ObservableProperty]
        private FamilySymbol _selectedDetailSymbol;

        /// <summary>
        /// Drives the "Include boundary lines" checkbox.
        /// Persists within the session; false when the window first opens.
        /// </summary>
        [ObservableProperty]
        private bool _includeBoundaryLines = false;

        /// <summary>
        /// Drives the "Group by drain points" checkbox.
        /// When enabled, creases are grouped by proximity and longest per start point is removed.
        /// </summary>
        [ObservableProperty]
        private bool _enableDrainGrouping = true;

        /// <summary>
        /// Drives the "Filter by Dijkstra path validity" checkbox (ticked by default).
        /// When enabled, every crease/boundary segment must be a valid first hop of
        /// some node's shortest route to its nearest minimum-elevation drain node.
        /// Runs on creases + boundary curves together, right after the horizontal filter,
        /// before minimum-length and drain-proximity grouping.
        /// </summary>
        [ObservableProperty]
        private bool _enableDijkstraPathFilter = true;

        /// <summary>
        /// Proximity radius for drain point grouping in millimeters (default 500mm).
        /// </summary>
        [ObservableProperty]
        private double _drainGroupingRadiusMm = 500.0;

        /// <summary>
        /// Drives the "Drop lines less than minimum" checkbox.
        /// When enabled, crease lines shorter than threshold are removed (applies to creases only).
        /// </summary>
        [ObservableProperty]
        private bool _enableMinimumLength = true;

        /// <summary>
        /// Minimum line length threshold in millimeters (default 500mm).
        /// Only applies to crease lines, not boundary lines.
        /// </summary>
        [ObservableProperty]
        private double _minimumLengthMm = 500.0;

        /// <summary>Populated after every Run. Null before the first run.</summary>
        [ObservableProperty]
        private RunSummary _summary;

        /// <summary>Controls summary bar visibility — false until first Run.</summary>
        [ObservableProperty]
        private bool _hasSummary = false;

        // --------------------------------------------------
        // Constructor
        // --------------------------------------------------

        public CreaserAdvViewModel(
            UIApplication  uiApp,
            Element        roof,
            LoggingService log)
        {
            _uiDoc = uiApp?.ActiveUIDocument
                ?? throw new ArgumentNullException(nameof(uiApp));

            _doc  = _uiDoc.Document;
            _roof = roof ?? throw new ArgumentNullException(nameof(roof));
            _log  = log  ?? throw new ArgumentNullException(nameof(log));

            LoadDetailSymbols();
        }

        // --------------------------------------------------
        // Load detail symbols
        // --------------------------------------------------

        private void LoadDetailSymbols()
        {
            DetailSymbols.Clear();

            foreach (FamilySymbol s in new DetailItemCollectorService(_doc, _log).Collect())
                DetailSymbols.Add(s);

            if (DetailSymbols.Count > 0)
                SelectedDetailSymbol = DetailSymbols[0];
        }

        // --------------------------------------------------
        // Run command
        // --------------------------------------------------

        [RelayCommand]
        private void Run()
        {
            if (SelectedDetailSymbol == null)
            {
                TaskDialog.Show("Creaser Advanced", "Please select a detail item.");
                return;
            }

            if (_doc.ActiveView is not ViewPlan planView || planView.GenLevel == null)
            {
                TaskDialog.Show("Creaser Advanced", "Run this command from a Plan View.");
                return;
            }

            _log.Info("─── Run started ───");

            using var tx = new Transaction(_doc, "Creaser Advanced V006 – Place Detail Items");
            tx.Start();

            // ── 1. Extract crease curves ──────────────────────────────────────
            var creaseService   = new RoofSharedTopFaceCreaseService(_log);
            IList<Curve> creaseCurves = creaseService.ExtractSharedTopFaceCreases(_roof);

            if (creaseCurves.Count == 0)
            {
                _log.Warning("No crease edges found — transaction rolled back.");
                tx.RollBack();
                UpdateSummary(0, 0, 0, 0);
                return;
            }

            // ── 1b. Filter out horizontal creases (same Z on both endpoints) ──
            var horizontalFilterSvc = new HorizontalCreaseFilterService(_log);
            creaseCurves = horizontalFilterSvc.FilterOutHorizontalCreases(creaseCurves);

            if (creaseCurves.Count == 0)
            {
                _log.Warning("No non-horizontal crease edges found — transaction rolled back.");
                tx.RollBack();
                UpdateSummary(0, 0, 0, 0);
                return;
            }

            // ── 2. Extract boundary curves (optional) ─────────────────────────
            IList<Curve> boundaryCurves = new List<Curve>();
            if (IncludeBoundaryLines)
            {
                boundaryCurves = creaseService.ExtractBoundaryLines(_roof);
                _log.Info($"Boundary lines extracted: {boundaryCurves.Count}");
            }

            // ── 2b. Filter by Dijkstra path validity (optional, ticked by default) ──
            // Runs on creases + boundary curves together, right after the horizontal
            // filter — this defines the working set before min-length/drain-grouping.
            if (EnableDijkstraPathFilter)
            {
                var dijkstraSvc = new DijkstraPathValidityService(_log);
                (creaseCurves, boundaryCurves) = dijkstraSvc.FilterByPathValidity(creaseCurves, boundaryCurves);

                if (creaseCurves.Count == 0 && boundaryCurves.Count == 0)
                {
                    _log.Warning("All curves failed the Dijkstra path validity check — transaction rolled back.");
                    tx.RollBack();
                    UpdateSummary(0, 0, 0, 0);
                    return;
                }
            }

            // ── 3. Project all curves to plan view ────────────────────────────
            // Projection returns curves+lines paired — a zero-length projection drops
            // the matching 3D curve too, so creaseCurves/creaseLines2d stay aligned.
            (creaseCurves, IList<Line> creaseLines2d) = ProjectToPlanView(creaseCurves, planView, "crease");

            IList<Line> boundaryLines2d;
            if (IncludeBoundaryLines)
                (boundaryCurves, boundaryLines2d) = ProjectToPlanView(boundaryCurves, planView, "boundary");
            else
                boundaryLines2d = new List<Line>();

            if (creaseLines2d.Count == 0 && boundaryLines2d.Count == 0)
            {
                _log.Warning("All lines collapsed during projection — transaction rolled back.");
                tx.RollBack();
                UpdateSummary(creaseCurves.Count, boundaryCurves.Count, 0, 0);
                return;
            }

            // ── 4. Filter creases by minimum length (optional, crease only) ────
            // Filters curves3d + lines2d together so DrainPointGroupingService (step 5)
            // still gets matching counts.
            if (EnableMinimumLength)
            {
                var minLengthSvc = new MinimumLengthFilterService(_log);
                (creaseCurves, creaseLines2d) =
                    minLengthSvc.FilterByMinimumLength(creaseCurves, creaseLines2d, MinimumLengthMm);

                if (creaseLines2d.Count == 0 && boundaryLines2d.Count == 0)
                {
                    _log.Warning("All crease lines filtered out by minimum length — transaction rolled back.");
                    tx.RollBack();
                    UpdateSummary(creaseCurves.Count, boundaryCurves.Count, 0, 0);
                    return;
                }
            }

            // ── 5. Group creases by drain proximity (optional) ──────────────────
            if (EnableDrainGrouping && creaseLines2d.Count > 0)
            {
                // Important: maintain 1:1 mapping between original curves and projected lines
                var drainSvc = new DrainPointGroupingService(_log);

                // Convert mm to Revit internal units for proximity radius
                double radiusFt = DrainGroupingRadiusMm / 304.8;

                creaseLines2d = drainSvc.FilterByDrainProximity(creaseCurves, creaseLines2d, radiusFt);

                if (creaseLines2d.Count == 0 && boundaryLines2d.Count == 0)
                {
                    _log.Warning("All crease lines filtered out by drain grouping — transaction rolled back.");
                    tx.RollBack();
                    UpdateSummary(creaseCurves.Count, boundaryCurves.Count, 0, 0);
                    return;
                }
            }

            // ── 6. Place detail items ─────────────────────────────────────────
            var allLines = creaseLines2d.Concat(boundaryLines2d).ToList();
            var (placed, failed) = new DetailItemPlacementService(_doc, planView)
                .PlaceAlongLines(allLines, SelectedDetailSymbol, _log);

            tx.Commit();

            // ── 7. Summary ────────────────────────────────────────────────────
            UpdateSummary(creaseCurves.Count, boundaryCurves.Count, placed, failed);
            _log.Info($"─── Run complete — created: {placed}  failed: {failed} ───");
        }

        // --------------------------------------------------
        // Clear log command
        // --------------------------------------------------

        [RelayCommand]
        private void ClearLog() => _log.Clear();

        // --------------------------------------------------
        // Copy log command
        // --------------------------------------------------

        [RelayCommand]
        private void CopyLog()
        {
            if (_log.Entries.Count == 0) return;

            var sb = new StringBuilder();
            foreach (LogEntry entry in _log.Entries)
                sb.AppendLine(entry.ToString());

            Clipboard.SetText(sb.ToString());
            _log.Info("Log copied to clipboard.");
        }

        // --------------------------------------------------
        // Private helpers
        // --------------------------------------------------

        /// <summary>
        /// Projects 3D curves to the plan view's Z elevation. Returns curves and
        /// lines index-aligned — a curve that projects to zero length is dropped
        /// from BOTH lists, so downstream steps (min-length filter, drain grouping)
        /// that need the original 3D curve stay in sync with the 2D line count.
        /// </summary>
        private (IList<Curve> Curves3d, IList<Line> Lines2d) ProjectToPlanView(
            IList<Curve> curves,
            ViewPlan     view,
            string       label)
        {
            double viewZ  = view.GenLevel.Elevation;
            double tol    = _doc.Application.ShortCurveTolerance;
            var    keptCurves = new List<Curve>();
            var    keptLines  = new List<Line>();
            int    skipped = 0;

            foreach (Curve curve in curves)
            {
                XYZ a = curve.GetEndPoint(0);
                XYZ b = curve.GetEndPoint(1);

                XYZ p1 = new XYZ(a.X, a.Y, viewZ);
                XYZ p2 = new XYZ(b.X, b.Y, viewZ);

                if (p1.DistanceTo(p2) < tol) { skipped++; continue; }

                keptCurves.Add(curve);
                keptLines.Add(Line.CreateBound(p1, p2));
            }

            if (skipped > 0)
                _log.Warning($"{skipped} {label} curve(s) projected to zero length — skipped.");

            _log.Info($"{char.ToUpper(label[0])}{label.Substring(1)} lines projected: {keptLines.Count}");
            return (keptCurves, keptLines);
        }

        private void UpdateSummary(int creasesFound, int boundaryFound, int created, int failed)
        {
            Summary    = new RunSummary(creasesFound, boundaryFound, created, failed);
            HasSummary = true;
        }
    }
}
