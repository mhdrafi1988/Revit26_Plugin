using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.SectionAutoRenumber.Handlers;
using Revit26_Plugin.SectionAutoRenumber.Models;
using Revit26_Plugin.SectionAutoRenumber.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Revit26_Plugin.SectionAutoRenumber.ViewModels
{
    public partial class SectionAutoRenumberViewModel : ObservableObject
    {
        // ─── dependencies ──────────────────────────────────────────────────
        private readonly UIDocument                  _uidoc;
        private readonly Document                    _doc;
        private readonly SectionAutoRenumberHandler  _handler;
        private readonly ExternalEvent               _externalEvent;
        private readonly Dispatcher                  _dispatcher;

        // ─── sheet list ────────────────────────────────────────────────────
        public ObservableCollection<ViewSheet> Sheets { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SectionRows))]
        private ViewSheet? _selectedSheet;

        partial void OnSelectedSheetChanged(ViewSheet? value) => RefreshGrid();

        // ─── inputs ────────────────────────────────────────────────────────
        [ObservableProperty] private string _startingNumber   = "1";
        [ObservableProperty] private string _verticalThreshold = "20";

        // ─── grid rows ─────────────────────────────────────────────────────
        public ObservableCollection<SectionRowViewModel> SectionRows { get; } = new();

        // ─── results ───────────────────────────────────────────────────────
        [ObservableProperty] private bool   _hasResults;
        [ObservableProperty] private int    _totalCount;
        [ObservableProperty] private int    _successCount;
        [ObservableProperty] private int    _failedCount;
        [ObservableProperty] private string _logText = string.Empty;

        // ─── constructor ───────────────────────────────────────────────────
        public SectionAutoRenumberViewModel(
            UIDocument                 uidoc,
            SectionAutoRenumberHandler handler,
            ExternalEvent              externalEvent)
        {
            _uidoc         = uidoc;
            _doc           = uidoc.Document;
            _handler       = handler;
            _externalEvent = externalEvent;
            _dispatcher    = Dispatcher.CurrentDispatcher;

            _handler.OnCompleted = OnRunCompleted;

            LoadSheets();
        }

        // ─── sheet loading ─────────────────────────────────────────────────
        private void LoadSheets()
        {
            var all = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber)
                .ToList();

            // active sheet first
            if (_doc.ActiveView is ViewSheet active)
            {
                var found = all.FirstOrDefault(s => s.Id == active.Id);
                if (found != null) { all.Remove(found); all.Insert(0, found); }
            }

            foreach (var s in all) Sheets.Add(s);
            SelectedSheet = Sheets.FirstOrDefault();
        }

        // ─── grid refresh ──────────────────────────────────────────────────
        private void RefreshGrid()
        {
            SectionRows.Clear();

            if (SelectedSheet is null) return;

            if (!double.TryParse(VerticalThreshold, out double mm) || mm <= 0) mm = 20;
            double thresholdFt = UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

            var rows = SectionAutoRenumberService.GetDisplayRows(_doc, SelectedSheet, thresholdFt);
            foreach (var r in rows) SectionRows.Add(r);
        }

        // ─── run command ───────────────────────────────────────────────────
        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            if (!int.TryParse(StartingNumber, out int startNum) || startNum <= 0) return;
            if (!double.TryParse(VerticalThreshold, out double mm) || mm <= 0)    return;

            double thresholdFt = UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

            _handler.TargetSheet  = SelectedSheet;
            _handler.StartNumber  = startNum;
            _handler.ThresholdFt  = thresholdFt;

            _externalEvent.Raise();
        }

        private bool CanRun() =>
            SelectedSheet != null &&
            int.TryParse(StartingNumber, out int n) && n > 0 &&
            double.TryParse(VerticalThreshold, out double t) && t > 0;

        // ─── completion callback (called on Revit thread → marshal to UI) ──
        private void OnRunCompleted(RenumberSummary summary)
        {
            _dispatcher.Invoke(() =>
            {
                TotalCount   = summary.Total;
                SuccessCount = summary.Success;
                FailedCount  = summary.Failed.Count;
                LogText      = $"Sheet: {summary.SheetNumber} — {summary.SheetName}\n"
                             + string.Join("\n", summary.LogLines);
                HasResults   = true;

                // refresh grid to show updated numbers
                RefreshGrid();
            });
        }
    }
}
