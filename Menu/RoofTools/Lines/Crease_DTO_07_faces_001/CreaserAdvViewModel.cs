// ==================================
// File: CreaserAdvViewModel.cs
// ==================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.CreaserAdv_V002.Services;

namespace Revit26_Plugin.CreaserAdv_V002.ViewModels
{
    /// <summary>
    /// ViewModel for Creaser Advanced.
    /// Uses solid topology to extract true roof creases
    /// (edges shared by two top faces only),
    /// then projects them into the active plan view
    /// before placing detail items.
    /// NOW SUPPORTS CURVED SURFACES!
    /// </summary>
    public partial class CreaserAdvViewModel : ObservableObject
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly Element _roof;
        private readonly LoggingService _log;

        // -----------------------------
        // UI-bound collections
        // -----------------------------

        public ObservableCollection<FamilySymbol> DetailSymbols { get; }
            = new ObservableCollection<FamilySymbol>();

        public ObservableCollection<LogEntry> LogEntries
            => _log.Entries;

        // -----------------------------
        // Selected detail symbol
        // -----------------------------

        private FamilySymbol _selectedDetailSymbol;
        public FamilySymbol SelectedDetailSymbol
        {
            get => _selectedDetailSymbol;
            set => SetProperty(ref _selectedDetailSymbol, value);
        }

        // -----------------------------
        // Constructor
        // -----------------------------

        public CreaserAdvViewModel(
            UIApplication uiApp,
            Element roof,
            LoggingService log)
        {
            _uiDoc = uiApp?.ActiveUIDocument
                ?? throw new ArgumentNullException(nameof(uiApp));

            _doc = _uiDoc.Document;
            _roof = roof ?? throw new ArgumentNullException(nameof(roof));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            LoadDetailSymbols();
        }

        // -----------------------------
        // Load available detail items
        // -----------------------------

        private void LoadDetailSymbols()
        {
            DetailSymbols.Clear();

            var collector =
                new DetailItemCollectorService(_doc, _log);

            foreach (FamilySymbol symbol in collector.Collect())
                DetailSymbols.Add(symbol);

            if (DetailSymbols.Count > 0)
                SelectedDetailSymbol = DetailSymbols[0];
        }

        // -----------------------------
        // MAIN RUN COMMAND
        // -----------------------------

        [RelayCommand]
        private void Run()
        {
            // -----------------------------
            // Validation
            // -----------------------------

            if (SelectedDetailSymbol == null)
            {
                TaskDialog.Show(
                    "Creaser Advanced",
                    "Please select a detail item.");
                return;
            }

            if (_doc.ActiveView is not ViewPlan planView ||
                planView.GenLevel == null)
            {
                TaskDialog.Show(
                    "Creaser Advanced",
                    "Run this command from a Plan View.");
                return;
            }

            _log.Info("Creaser Advanced started - CURVED SURFACE SUPPORT ENABLED");

            using (var tx =
                   new Transaction(_doc, "Creaser Advanced – Place Creases"))
            {
                tx.Start();

                // ----------------------------------
                // 1. Extract TRUE roof creases (3D)
                //    USING ENHANCED SERVICE FOR CURVED SURFACES
                // ----------------------------------

                var creaseService =
                    new RoofSharedTopFaceCreaseService(_log);

                IList<Curve> creaseCurves3d =
                    creaseService.ExtractSharedTopFaceCreases(_roof);

                if (creaseCurves3d.Count == 0)
                {
                    _log.Warning("No shared top-face creases found on curved surfaces.");
                    tx.RollBack();
                    return;
                }

                _log.Info($"Found {creaseCurves3d.Count} crease curves in 3D");

                // ----------------------------------
                // 2. Project creases into plan view
                //    USING NEW CURVED CURVE PROJECTION SERVICE
                // ----------------------------------

                var projectionService = new CurvedCurveProjectionService(_log);
                IList<Curve> creaseCurves2d = projectionService.ProjectToPlan(creaseCurves3d, planView);

                if (creaseCurves2d.Count == 0)
                {
                    _log.Warning("All creases collapsed during plan projection.");
                    tx.RollBack();
                    return;
                }

                _log.Info($"Successfully projected {creaseCurves2d.Count} curves to plan");

                // ----------------------------------
                // 3. Place detail items
                //    USING ENHANCED PLACEMENT SERVICE
                // ----------------------------------

                var placer =
                    new DetailItemPlacementService(
                        _doc,
                        planView);

                placer.PlaceAlongCurves(
                    creaseCurves2d,
                    SelectedDetailSymbol,
                    _log);

                tx.Commit();
            }

            _log.Info("Creaser Advanced completed successfully with curved surface support.");
        }

        // -----------------------------
        // Clear log command
        // -----------------------------

        [RelayCommand]
        private void ClearLog()
        {
            _log.Clear();
        }
    }
}