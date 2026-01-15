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

            _log.Info("Creaser Advanced started.");

            using (var tx =
                   new Transaction(_doc, "Creaser Advanced – Place Creases"))
            {
                tx.Start();

                // ----------------------------------
                // 1. Extract TRUE roof creases (3D)
                //    Shared by two top faces only
                // ----------------------------------

                var creaseService =
                    new RoofSharedTopFaceCreaseService(_log);

                IList<Line> creaseLines3d =
                    creaseService.ExtractSharedTopFaceCreases(_roof);

                if (creaseLines3d.Count == 0)
                {
                    _log.Warning("No shared top-face creases found.");
                    tx.RollBack();
                    return;
                }

                // ----------------------------------
                // 2. Project creases into plan view
                //    (MANDATORY for detail items)
                // ----------------------------------

                double viewZ = planView.GenLevel.Elevation;
                double tol = _doc.Application.ShortCurveTolerance;

                var creaseLines2d = new List<Line>();

                foreach (Line line3d in creaseLines3d)
                {
                    XYZ a = line3d.GetEndPoint(0);
                    XYZ b = line3d.GetEndPoint(1);

                    XYZ p1 = new XYZ(a.X, a.Y, viewZ);
                    XYZ p2 = new XYZ(b.X, b.Y, viewZ);

                    if (p1.DistanceTo(p2) < tol)
                        continue;

                    creaseLines2d.Add(Line.CreateBound(p1, p2));
                }

                if (creaseLines2d.Count == 0)
                {
                    _log.Warning("All creases collapsed during plan projection.");
                    tx.RollBack();
                    return;
                }

                // ----------------------------------
                // 3. Place detail items
                // ----------------------------------

                var placer =
                    new DetailItemPlacementService(
                        _doc,
                        planView);

                placer.PlaceAlongLines(
                    creaseLines2d,
                    SelectedDetailSymbol,
                    _log);

                tx.Commit();
            }

            _log.Info("Creaser Advanced completed.");
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
