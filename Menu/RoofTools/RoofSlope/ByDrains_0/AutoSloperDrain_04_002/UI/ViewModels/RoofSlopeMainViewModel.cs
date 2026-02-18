// =====================================================
// File: RoofSlopeMainViewModel.cs
// =====================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.V4_02.Application.Contexts;
using Revit22_Plugin.V4_02.Application.Coordinators;
using Revit22_Plugin.V4_02.Domain.Factories;
using Revit22_Plugin.V4_02.Domain.Models;
using Revit22_Plugin.V4_02.Domain.Services;
using Revit22_Plugin.V4_02.Infrastructure.Logging;
using Revit22_Plugin.V4_02.Infrastructure.Revit;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Revit22_Plugin.V4_02.UI.ViewModels
{
    public class RoofSlopeMainViewModel :
        INotifyPropertyChanged,
        IAutoSlopeLogger
    {
        private readonly UIApplication _uiApp;
        private readonly RoofBase _roof;

        public RoofData RoofData { get; }

        public ObservableCollection<DrainItem> AllDrains { get; }
            = new ObservableCollection<DrainItem>();

        public ObservableCollection<string> SlopePresets { get; }
            = new ObservableCollection<string>
            {
                "1.00", "1.25", "1.50", "1.75", "2.00"
            };

        private string _slopeInput = "1.00";
        public string SlopeInput
        {
            get => _slopeInput;
            set { _slopeInput = value; OnPropertyChanged(); }
        }

        public Action CloseWindow { get; set; }

        public RoofSlopeMainViewModel(
            UIApplication uiApp,
            RoofBase roof)
        {
            _uiApp = uiApp;
            _roof = roof;

            RoofData = RoofDataFactory.Create(roof);
            LoadDrains();
        }

        // =====================================================
        // ENTRY POINT FROM UI (IMPORTANT)
        // =====================================================
        public void ApplySlopesFromUI()
        {
            ApplySlopes();
        }

        // =====================================================
        // CORE LOGIC (UNCHANGED)
        // =====================================================
        private void ApplySlopes()
        {
            if (!double.TryParse(SlopeInput, out double slope) || slope <= 0)
            {
                Warn("Invalid slope value.");
                return;
            }

            var selected = AllDrains.Where(d => d.IsSelected).ToList();
            if (!selected.Any())
            {
                Warn("No drains selected.");
                return;
            }

            var context = new AutoSlopeContext
            {
                RoofData = RoofData,
                SelectedDrains = selected,
                SlopePercent = slope,
                ThresholdMeters = 0,
                Logger = this
            };

            var coordinator = new AutoSlopeCoordinator();

            RevitTransactionService.Run(
                _roof.Document,
                "AutoSlope",
                () =>
                {
                    coordinator.Execute(context);
                });

            Info("Slope application completed.");
        }

        private void LoadDrains()
        {
            var detector = new DrainDetectionService();
            var drains = detector.DetectDrainsFromRoof(
                RoofData.Roof,
                RoofData.TopFace);

            AllDrains.Clear();
            foreach (var d in drains)
                AllDrains.Add(d);
        }

        // =====================================================
        // LOGGING
        // =====================================================
        public void Info(string message) => AppendLog(message);
        public void Warn(string message) => AppendLog("⚠ " + message);
        public void Error(string message) => AppendLog("❌ " + message);

        private string _logText = "";
        public string LogText
        {
            get => _logText;
            set { _logText = value; OnPropertyChanged(); }
        }

        private void AppendLog(string msg)
        {
            LogText += msg + Environment.NewLine;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(
            [CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
