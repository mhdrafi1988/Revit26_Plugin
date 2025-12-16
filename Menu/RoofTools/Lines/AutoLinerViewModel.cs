    using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoLiner_V01.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.AutoLiner_V01.ViewModels
{
    public partial class AutoLinerViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;

        private readonly RoofBase _selectedRoof;

        // ================= UI =================
        public ObservableCollection<string> TopLogs { get; } = new();
        public ObservableCollection<string> BottomLogs { get; } = new();

        public ObservableCollection<FamilySymbol> LineFamilies { get; }
            = new();

        [ObservableProperty]
        private FamilySymbol selectedLineFamily;

        [ObservableProperty]
        private double minSegmentLengthMm = 500;

        public AutoLinerViewModel(
            UIApplication uiApp,
            RoofBase roof)
        {
            _uiApp = uiApp;
            _uiDoc = uiApp.ActiveUIDocument;
            _doc = _uiDoc.Document;

            _selectedRoof = roof;

            BottomLogs.Add($"Roof selected: {roof.Id.Value}");

            LoadDetailLineFamilies();
        }

        // ================= LOAD DETAIL ITEMS =================
        private void LoadDetailLineFamilies()
        {
            LineFamilies.Clear();

            var symbols =
                DetailItemCollectorService
                    .GetLineBasedDetailItemSymbols(
                        _doc,
                        _uiDoc.ActiveView);

            foreach (var s in symbols)
                LineFamilies.Add(s);

            BottomLogs.Add(
                $"Line-based detail families loaded: {LineFamilies.Count}");

            SelectedLineFamily = LineFamilies.FirstOrDefault();
        }

        // ================= RUN =================
        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            TopLogs.Clear();
            BottomLogs.Clear();

            if (_selectedRoof == null || SelectedLineFamily == null)
            {
                TopLogs.Add("Missing roof or line family.");
                return;
            }

            TopLogs.Add("Running AutoLiner...");

            // Basic placement test: build a simple two-point path in model coordinates
            var path = new System.Collections.Generic.List<XYZ>
            {
                new XYZ(0, 0, 0),
                new XYZ(10, 0, 0) // 10 feet long segment for test
            };

            var placer = new FlowLinePlacementService();
            int created = 0;
            try
            {
                created = placer.PlaceFlowLines(_doc, _uiDoc.ActiveView, SelectedLineFamily, path, MinSegmentLengthMm);
            }
            catch (Exception ex)
            {
                TopLogs.Add($"Error placing detail items: {ex.Message}");
            }

            TopLogs.Add($"Placed detail line instances: {created}");
        }

        private bool CanRun()
        {
            return _selectedRoof != null && SelectedLineFamily != null;
        }
    }
}
