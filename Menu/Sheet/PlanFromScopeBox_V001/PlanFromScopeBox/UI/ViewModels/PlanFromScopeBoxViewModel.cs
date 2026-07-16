using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Threading;
using System.ComponentModel; // <-- Add this using directive
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.PlanFromScopeBox.V001.Core.Engine;
using Revit26_Plugin.PlanFromScopeBox.V001.Core.Models;
using Revit26_Plugin.PlanFromScopeBox.V001.Core.Services;
using Revit26_Plugin.PlanFromScopeBox.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.PlanFromScopeBox.V001.Infrastructure.Helpers;
using Revit26_Plugin.PlanFromScopeBox.V001.Infrastructure.Settings;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.PlanFromScopeBox.V001.UI.ViewModels
{
    /// <summary>
    /// Drives the Plan From Scope Box window. Reads scope boxes/level/title blocks on open,
    /// measures the selected title block via the orchestrating ExternalEvent, and runs
    /// generation through the same event. Persists user defaults (H2) and auto-saves logs (H3).
    /// </summary>
    public partial class PlanFromScopeBoxViewModel : ObservableObject, IDisposable
    {
        public const string Version = "V001";
        private const int MaxLogEntries = 2000;

        private readonly UIApplication _uiApp;
        private readonly Dispatcher _dispatcher;
        private readonly ExternalEvent _externalEvent;
        private readonly PlanFromScopeBoxEventHandler _handler;

        private readonly ScopeBoxScanner _scanner = new();
        private readonly SettingsService _settingsService = new();

        private ElementId _levelId = ElementId.InvalidElementId;

        // Measured sheet size (mm) — feeds both the Fits preview and the generation
        // request. Defaults to A0 until a measure completes (B).
        private double _sheetWmm = SheetLayout.A0WidthMm;
        private double _sheetHmm = SheetLayout.A0HeightMm;

        // Title-block sheet size read back from the measure event (display only).
        [ObservableProperty] private string _sheetSizeDisplay = "(select a title block)";

        // ── Grid ──────────────────────────────────────────────────────────────────
        public ObservableCollection<ScopeBoxRow> ScopeBoxes { get; } = new();
        public ICollectionView ScopeBoxesView { get; }
        [ObservableProperty] private string _filterText = string.Empty;

        // ── Settings (strings for numeric inputs → safe mid-typing parse, M4) ──────
        public ObservableCollection<TitleBlockOption> TitleBlocks { get; } = new();
        [ObservableProperty] private TitleBlockOption _selectedTitleBlock;

        [ObservableProperty] private string _scaleText = "100";
        [ObservableProperty] private string _marginText = "10";
        [ObservableProperty] private string _gutterText = "8";
        [ObservableProperty] private string _clearRightText = "0";
        [ObservableProperty] private string _clearBottomText = "0";
        [ObservableProperty] private string _activeLevelDisplay = "(none)";

        // ── Naming ────────────────────────────────────────────────────────────────
        [ObservableProperty] private string _sheetNumberPattern = "AP-{Increment}";
        [ObservableProperty] private string _sheetNamePattern = "{ScopeBoxName}";
        [ObservableProperty] private string _viewNumberPattern = "{Increment}";
        [ObservableProperty] private string _viewNamePattern = "{ScopeBoxName} - Plan";

        // ── Options ───────────────────────────────────────────────────────────────
        [ObservableProperty] private bool _openSheetsAfter = true;

        // ── Metrics ───────────────────────────────────────────────────────────────
        [ObservableProperty] private int _plansCreated;
        [ObservableProperty] private int _sheetsCreated;
        [ObservableProperty] private int _skipped;
        [ObservableProperty] private bool _isBusy;

        // ── Log ───────────────────────────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        /// <summary>Bound to the log ListBox's SelectedItems via code-behind for Copy Selected (L2).</summary>
        public IList<LogEntry> SelectedLogEntries { get; set; } = new List<LogEntry>();

        public PlanFromScopeBoxViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _handler = new PlanFromScopeBoxEventHandler
            {
                Log = (lvl, msg) => _dispatcher.Invoke(() => Log(lvl, msg))
            };
            _externalEvent = ExternalEvent.Create(_handler);

            ScopeBoxesView = CollectionViewSource.GetDefaultView(ScopeBoxes);
            ScopeBoxesView.Filter = FilterRow;

            LoadSettings();
            LoadDocumentData();
        }

        // Design-time ctor.
        public PlanFromScopeBoxViewModel()
        {
            ScopeBoxesView = CollectionViewSource.GetDefaultView(ScopeBoxes);
        }

        // ── Parse helpers (M4) ──────────────────────────────────────────────────────
        private int ScaleDenominator =>
            int.TryParse(ScaleText, out int v) && v > 0 ? v : 100;
        private double MarginMm =>
            double.TryParse(MarginText, out double v) && v >= 0 ? v : 0;
        private double GutterMm =>
            double.TryParse(GutterText, out double v) && v >= 0 ? v : 0;
        private double ClearRightMm =>
            double.TryParse(ClearRightText, out double v) && v >= 0 ? v : 0;
        private double ClearBottomMm =>
            double.TryParse(ClearBottomText, out double v) && v >= 0 ? v : 0;

        partial void OnFilterTextChanged(string value) => ScopeBoxesView?.Refresh();
        partial void OnScaleTextChanged(string value) => RecomputeFits();
        partial void OnMarginTextChanged(string value) => RecomputeFits();
        partial void OnClearRightTextChanged(string value) => RecomputeFits();
        partial void OnClearBottomTextChanged(string value) => RecomputeFits();
        // Gutter deliberately excluded: it affects packing between views, not single-view fit.

        partial void OnSelectedTitleBlockChanged(TitleBlockOption value)
        {
            // Re-measure the newly selected title block (API context via event).
            if (value != null) RaiseMeasure();
        }

        private bool FilterRow(object obj)
        {
            if (obj is not ScopeBoxRow row) return false;
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            return row.Name.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LoadDocumentData()
        {
            try
            {
                Document doc = _uiApp.ActiveUIDocument.Document;
                View active = doc.ActiveView;

                Level level = _scanner.GetActiveLevel(doc, active);
                if (level == null)
                {
                    Log(LogLevel.Error, "Active view has no associated level. Open a plan view and reopen.");
                    ActiveLevelDisplay = "(no level — open a plan view)";
                }
                else
                {
                    _levelId = level.Id;
                    ActiveLevelDisplay = level.Name;
                }

                string levelName = level?.Name ?? string.Empty;
                foreach (ScopeBoxRow row in _scanner.GetScopeBoxes(doc, levelName))
                    ScopeBoxes.Add(row);

                LoadTitleBlocks(doc);
                RecomputeFits();

                Log(LogLevel.Info, $"Found {ScopeBoxes.Count} scope boxes · active level {ActiveLevelDisplay}");
                // Initial title-block measure already raised by the SelectedTitleBlock
                // setter inside LoadTitleBlocks (F) — no second raise here.
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to read document: {ex.Message}");
            }
        }

        private void LoadTitleBlocks(Document doc)
        {
            var tbs = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .Cast<ElementType>()
                .Select(t => new TitleBlockOption
                {
                    TypeId = t.Id.Value,
                    Display = $"{t.FamilyName} — {t.Name}"
                })
                .OrderBy(o => o.Display)
                .ToList();

            foreach (TitleBlockOption tb in tbs) TitleBlocks.Add(tb);

            // Restore last-used title block if still present, else first.
            SelectedTitleBlock =
                TitleBlocks.FirstOrDefault(t => t.Display == _restoreTitleBlockDisplay)
                ?? TitleBlocks.FirstOrDefault();
        }

        /// <summary>Recomputes the per-row "Fits on A0" flag from current scale/margin/clear zone.</summary>
        private void RecomputeFits()
        {
            var (usableW, usableH) = SheetLayout.Usable(_sheetWmm, _sheetHmm, MarginMm, ClearRightMm, ClearBottomMm);
            int denom = ScaleDenominator;

            foreach (ScopeBoxRow row in ScopeBoxes)
            {
                var (wMm, hMm) = SheetLayout.Footprint(row.WidthM, row.HeightM, denom);
                row.Fits = SheetLayout.Fits(wMm, hMm, usableW, usableH);
            }
        }

        // ── Measure event ───────────────────────────────────────────────────────────
        private void RaiseMeasure()
        {
            if (SelectedTitleBlock == null) return;

            _handler.Enqueue(new MeasureTitleBlockRequest
            {
                TitleBlockTypeId = new ElementId(SelectedTitleBlock.TypeId),
                OnComplete = ext => _dispatcher.Invoke(() => OnMeasured(ext))
            });

            ExternalEventRequest r = _externalEvent.Raise();
            if (r != ExternalEventRequest.Accepted)
            {
                _handler.ClearPending();
                Log(LogLevel.Warning, $"Title-block measure request not accepted ({r}).");
            }
        }

        private void OnMeasured(TitleBlockExtents ext)
        {
            if (ext.Measured)
            {
                _sheetWmm = ext.SheetWidthMm;
                _sheetHmm = ext.SheetHeightMm;
                SheetSizeDisplay = $"{ext.SheetWidthMm:0} × {ext.SheetHeightMm:0} mm";
            }
            else
            {
                _sheetWmm = SheetLayout.A0WidthMm;
                _sheetHmm = SheetLayout.A0HeightMm;
                SheetSizeDisplay = "(size unavailable — assuming A0)";
            }
            RecomputeFits();
        }

        // ── Commands ────────────────────────────────────────────────────────────────
        [RelayCommand]
        private void SelectAll()
        {
            foreach (ScopeBoxRow row in ScopeBoxesView.Cast<ScopeBoxRow>())
                row.IsSelected = true;
        }

        [RelayCommand]
        private void ClearSelection()
        {
            // Applies to visible/filtered rows only, matching Select All (G).
            foreach (ScopeBoxRow row in ScopeBoxesView.Cast<ScopeBoxRow>())
                row.IsSelected = false;
        }

        [RelayCommand]
        private void Refresh()
        {
            ScopeBoxes.Clear();
            TitleBlocks.Clear();
            LoadDocumentData();
        }

        [RelayCommand]
        private void CopyAllLogs() => TrySetClipboard(string.Join(Environment.NewLine, LogEntries));

        [RelayCommand]
        private void CopySelectedLogs()
        {
            if (SelectedLogEntries == null || SelectedLogEntries.Count == 0) return;
            TrySetClipboard(string.Join(Environment.NewLine, SelectedLogEntries));
        }

        [RelayCommand]
        private void ClearLogs() => LogEntries.Clear();

        [RelayCommand]
        private void ExportLogs()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = LogExporter.BuildFileName(),
                Filter = "Text file (*.txt)|*.txt",
                InitialDirectory = _settingsService.ToolDataDirectory
            };
            if (dlg.ShowDialog() == true)
            {
                bool ok = LogExporter.WriteTo(LogEntries, dlg.FileName);
                Log(ok ? LogLevel.Success : LogLevel.Warning,
                    ok ? $"Log exported → {dlg.FileName}" : "Log export failed.");
            }
        }

        [RelayCommand]
        private void CreatePlansAndSheets()
        {
            if (IsBusy) return;

            List<ScopeBoxRow> selected = ScopeBoxes.Where(r => r.IsSelected).ToList();
            if (selected.Count == 0) { Log(LogLevel.Warning, "No scope boxes selected."); return; }
            if (_levelId == ElementId.InvalidElementId) { Log(LogLevel.Error, "No active level resolved — cannot create plans."); return; }
            if (SelectedTitleBlock == null) { Log(LogLevel.Error, "No title block selected."); return; }

            IsBusy = true;
            PlansCreated = SheetsCreated = Skipped = 0;

            var payload = new GenerationRequest
            {
                SelectedRows = selected,
                LevelId = _levelId,
                TitleBlockTypeId = new ElementId(SelectedTitleBlock.TypeId),
                ScaleDenominator = ScaleDenominator,
                MarginMm = MarginMm,
                GutterMm = GutterMm,
                TbClearRightMm = ClearRightMm,
                TbClearBottomMm = ClearBottomMm,
                OpenSheetsAfter = OpenSheetsAfter,
                SheetNumberPattern = SheetNumberPattern,
                SheetNamePattern = SheetNamePattern,
                ViewNumberPattern = ViewNumberPattern,
                ViewNamePattern = ViewNamePattern,
                SheetWidthMm = _sheetWmm,
                SheetHeightMm = _sheetHmm
            };

            _handler.Enqueue(new GenerateRequest
            {
                Payload = payload,
                OnComplete = summary => _dispatcher.Invoke(() => OnRunComplete(summary))
            });

            Log(LogLevel.Info, $"{selected.Count} selected · scale 1:{ScaleDenominator} · {SelectedTitleBlock.Display}");

            ExternalEventRequest req = _externalEvent.Raise();
            if (req != ExternalEventRequest.Accepted)
            {
                // C2 failsafe: clear the stale queued request and re-enable the UI.
                _handler.ClearPending();
                IsBusy = false;
                Log(LogLevel.Error, $"Revit did not accept the request ({req}). Try again.");
            }
        }

        private void OnRunComplete(GenerationSummary summary)
        {
            PlansCreated = summary.PlansCreated;
            SheetsCreated = summary.SheetsCreated;
            Skipped = summary.Skipped;
            IsBusy = false;

            Log(LogLevel.Info,
                $"Done · {summary.PlansCreated} plans · {summary.SheetsCreated} sheets · {summary.Skipped} skipped");

            // Auto-save logs silently to the tool's AppData folder (H3).
            string path = LogExporter.Write(LogEntries, _settingsService.ToolDataDirectory);
            if (path != null) Log(LogLevel.Info, $"Log auto-saved → {path}");

            SaveSettings();
        }

        // ── Settings (H2) ───────────────────────────────────────────────────────────
        private string _restoreTitleBlockDisplay = string.Empty;

        private void LoadSettings()
        {
            PlanFromScopeBoxSettings s = _settingsService.Load();
            ScaleText = s.ScaleDenominator.ToString();
            MarginText = s.MarginMm.ToString();
            GutterText = s.GutterMm.ToString();
            ClearRightText = s.TbClearRightMm.ToString();
            ClearBottomText = s.TbClearBottomMm.ToString();
            OpenSheetsAfter = s.OpenSheetsAfter;
            SheetNumberPattern = s.SheetNumberPattern;
            SheetNamePattern = s.SheetNamePattern;
            ViewNumberPattern = s.ViewNumberPattern;
            ViewNamePattern = s.ViewNamePattern;
            _restoreTitleBlockDisplay = s.LastTitleBlockDisplay;
        }

        public void SaveSettings()
        {
            _settingsService.Save(new PlanFromScopeBoxSettings
            {
                ScaleDenominator = ScaleDenominator,
                MarginMm = MarginMm,
                GutterMm = GutterMm,
                TbClearRightMm = ClearRightMm,
                TbClearBottomMm = ClearBottomMm,
                OpenSheetsAfter = OpenSheetsAfter,
                SheetNumberPattern = SheetNumberPattern,
                SheetNamePattern = SheetNamePattern,
                ViewNumberPattern = ViewNumberPattern,
                ViewNamePattern = ViewNamePattern,
                LastTitleBlockDisplay = SelectedTitleBlock?.Display ?? string.Empty
            });
        }

        // ── Logging ───────────────────────────────────────────────────────────────
        private void Log(LogLevel level, string message)
        {
            LogEntries.Add(new LogEntry(level, message));
            while (LogEntries.Count > MaxLogEntries) LogEntries.RemoveAt(0);
        }

        private static void TrySetClipboard(string text)
        {
            try { System.Windows.Clipboard.SetText(text); } catch { }
        }

        public void Dispose()
        {
            SaveSettings();
            _externalEvent?.Dispose();
        }
    }

    /// <summary>Title block type option for the dropdown.</summary>
    public sealed class TitleBlockOption
    {
        public long TypeId { get; init; }
        public string Display { get; init; } = string.Empty;
        public override string ToString() => Display;
    }
}
