// ==================================
// File: CreaserAdvViewModel.cs
// Namespace: Revit26_Plugin.CreaserAdv_V003_01
// ==================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.CreaserAdv_V003_01.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Revit26_Plugin.CreaserAdv_V003_01.ViewModels
{
    /// <summary>
    /// ViewModel for Creaser Advanced.
    /// Extracts true roof creases (edges shared by two top faces),
    /// projects them into the active plan view,
    /// then places detail items along each crease.
    /// </summary>
    public partial class CreaserAdvViewModel : ObservableObject
    {
        // --------------------------------------------------
        // Fields
        // --------------------------------------------------

        private readonly UIDocument _uiDoc;
        private readonly Document  _doc;
        private readonly Element   _roof;
        private readonly LoggingService _log;

        // --------------------------------------------------
        // UI-bound collections
        // --------------------------------------------------

        public ObservableCollection<FamilySymbol> DetailSymbols { get; }
            = new ObservableCollection<FamilySymbol>();

        public ObservableCollection<LogEntry> LogEntries => _log.Entries;

        // --------------------------------------------------
        // Selected detail symbol
        // --------------------------------------------------

        [ObservableProperty]
        private FamilySymbol _selectedDetailSymbol;

        // --------------------------------------------------
        // Constructor
        // --------------------------------------------------

        public CreaserAdvViewModel(
            UIApplication uiApp,
            Element       roof,
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
        // Load available detail items
        // --------------------------------------------------

        private void LoadDetailSymbols()
        {
            DetailSymbols.Clear();

            var collector = new DetailItemCollectorService(_doc, _log);

            foreach (FamilySymbol symbol in collector.Collect())
                DetailSymbols.Add(symbol);

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

            _log.Info("Creaser Advanced started.");

            using var tx = new Transaction(_doc, "Creaser Advanced – Place Creases");
            tx.Start();

            // 1. Extract true roof creases (3D) — shared top-face edges only
            var creaseService  = new RoofSharedTopFaceCreaseService(_log);
            IList<Curve> raw3d = creaseService.ExtractSharedTopFaceCreases(_roof);

            if (raw3d.Count == 0)
            {
                _log.Warning("No shared top-face creases found.");
                tx.RollBack();
                return;
            }

            // 2. Project into the plan view plane
            IList<Line> creaseLines2d = ProjectToPlanView(raw3d, planView);

            if (creaseLines2d.Count == 0)
            {
                _log.Warning("All creases collapsed during plan projection.");
                tx.RollBack();
                return;
            }

            // 3. Place detail items
            new DetailItemPlacementService(_doc, planView)
                .PlaceAlongLines(creaseLines2d, SelectedDetailSymbol, _log);

            tx.Commit();
            _log.Info("Creaser Advanced completed.");
        }

        // --------------------------------------------------
        // Clear log command
        // --------------------------------------------------

        [RelayCommand]
        private void ClearLog() => _log.Clear();

        // --------------------------------------------------
        // Private helpers
        // --------------------------------------------------

        /// <summary>
        /// Projects a list of 3-D curves onto the plan-view Z elevation.
        /// Non-linear curves are linearised by connecting their endpoints;
        /// degenerate results shorter than <see cref="Document.Application.ShortCurveTolerance"/>
        /// are discarded.
        /// </summary>
        private IList<Line> ProjectToPlanView(IList<Curve> curves3d, ViewPlan view)
        {
            double viewZ = view.GenLevel.Elevation;
            double tol   = _doc.Application.ShortCurveTolerance;

            var result = new List<Line>();

            foreach (Curve curve in curves3d)
            {
                XYZ a = curve.GetEndPoint(0);
                XYZ b = curve.GetEndPoint(1);

                XYZ p1 = new XYZ(a.X, a.Y, viewZ);
                XYZ p2 = new XYZ(b.X, b.Y, viewZ);

                if (p1.DistanceTo(p2) < tol)
                    continue;

                result.Add(Line.CreateBound(p1, p2));
            }

            _log.Info($"Creases projected to plan: {result.Count}");
            return result;
        }
    }
}
