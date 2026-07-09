// File: AutoSlopeDrainViewModel.cs
// Location: UI/ViewModels/
// Changes vs CSV version (MainViewModel.cs):
//   REPLACED custom RelayCommand class -> CommunityToolkit.Mvvm.Input.RelayCommand
//   REPLACED LogText (string) -> ObservableCollection<LogEntry> (Shared.Models)
//   REMOVED  IncludeVertexDetails toggle (export always writes detailed + summary CSV)
//   REMOVED  OK / Cancel / Change Roof (window now closes via titlebar only, per approved UI)
//   CHANGED  ApplySlopes -> RunAutoSlope, now raises AutoSlopeDrainEventManager.Event
//            instead of calling RoofSlopeProcessorService directly (window is modeless).
//   KEPT     Select All / None / Invert, size filter, DataGrid sorting (built-in via
//            ICollectionView + DataGrid column SortMemberPath — no VM code needed).

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoSlopeByDrain.V004.Core.Models;
using Revit26_Plugin.AutoSlopeByDrain.V004.Core.Services;
using Revit26_Plugin.AutoSlopeByDrain.V004.Infrastructure.ExternalEvents;
using Revit26_Plugin.AutoSlopeByDrain.V004.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;

namespace Revit26_Plugin.AutoSlopeByDrain.V004.UI.ViewModels
{
    public class AutoSlopeDrainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── RunState ──────────────────────────────────────────────────────────
        private enum RunState { Ready, Running, Done }
        private RunState _state = RunState.Ready;
        private RunState State
        {
            get => _state;
            set
            {
                _state = value;
                Raise(nameof(StatusMessage));
                Raise(nameof(LongestPathDisplay));
                Raise(nameof(HighestElevationDisplay));
                RunCommand.NotifyCanExecuteChanged();
            }
        }
        private bool IsRunning => _state == RunState.Running;
        private bool IsComplete => _state == RunState.Done;

        public string StatusMessage => _state switch
        {
            RunState.Running => "Processing...",
            RunState.Done => "Completed",
            _ => "Ready"
        };

        // ── Roof / drains ─────────────────────────────────────────────────────
        public UIDocument UIDoc { get; }
        public UIApplication App { get; }
        private readonly RoofData _roofData;
        public ElementId RoofId => _roofData.Roof.Id;

        public ObservableCollection<DrainItem> AllDrains { get; } = new ObservableCollection<DrainItem>();
        public ObservableCollection<string> SizeFilters { get; } = new ObservableCollection<string>();
        public ICollectionView FilteredDrainsView { get; }

        private string _roofSubtitle;
        public string RoofSubtitle
        {
            get => _roofSubtitle;
            set { _roofSubtitle = value; Raise(); }
        }

        private string _selectedSizeFilter = "All";
        public string SelectedSizeFilter
        {
            get => _selectedSizeFilter;
            set
            {
                _selectedSizeFilter = value;
                Raise();
                FilteredDrainsView.Refresh();
                UpdateSelectedCount();
            }
        }

        private int _selectedDrainsCount;
        public int SelectedDrainsCount
        {
            get => _selectedDrainsCount;
            set { _selectedDrainsCount = value; Raise(); }
        }

        // ── Slope inputs ──────────────────────────────────────────────────────
        public List<string> SlopeOptions { get; } = new List<string> { "1.0", "1.5", "2.0", "2.5", "3.0" };

        private string _slopeInput = "1.5";
        public string SlopeInput
        {
            get => _slopeInput;
            set { _slopeInput = value; Raise(); }
        }

        private string _connectionThresholdInput = "30";
        public string ConnectionThresholdInput
        {
            get => _connectionThresholdInput;
            set { _connectionThresholdInput = value; Raise(); }
        }

        private string _pathSampleCountInput = "50";
        public string PathSampleCountInput
        {
            get => _pathSampleCountInput;
            set { _pathSampleCountInput = value; Raise(); }
        }

        // ── Export ────────────────────────────────────────────────────────────
        private string _exportFolderPath;
        public string ExportFolderPath
        {
            get => _exportFolderPath;
            set { _exportFolderPath = value; Raise(); }
        }

        private bool _exportToCsv = true;
        public bool ExportToCsv
        {
            get => _exportToCsv;
            set { _exportToCsv = value; Raise(); }
        }

        // ── Log ───────────────────────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        // ── Results / metrics ─────────────────────────────────────────────────
        private double _longestPathM;
        public double LongestPath_m
        {
            get => _longestPathM;
            set { _longestPathM = value; Raise(); Raise(nameof(LongestPathDisplay)); }
        }
        public string LongestPathDisplay => IsComplete ? LongestPath_m.ToString("F2") : "N/A";

        private double _highestElevationMm;
        public double HighestElevation_mm
        {
            get => _highestElevationMm;
            set { _highestElevationMm = value; Raise(); Raise(nameof(HighestElevationDisplay)); }
        }
        public string HighestElevationDisplay => IsComplete ? HighestElevation_mm.ToString("F0") : "N/A";

        private int _runDurationSec;
        public int RunDuration_sec
        {
            get => _runDurationSec;
            set { _runDurationSec = value; Raise(); }
        }

        private int _finalDrainCount;
        public int FinalDrainCount
        {
            get => _finalDrainCount;
            set { _finalDrainCount = value; Raise(); }
        }

        private AutoSlopeDrainResult _lastResult;

        // ── Commands ──────────────────────────────────────────────────────────
        private RelayCommand _selectAllCommand;
        public RelayCommand SelectAllCommand => _selectAllCommand ??= new RelayCommand(SelectAllDrains);

        private RelayCommand _selectNoneCommand;
        public RelayCommand SelectNoneCommand => _selectNoneCommand ??= new RelayCommand(SelectNoneDrains);

        private RelayCommand _invertSelectionCommand;
        public RelayCommand InvertSelectionCommand => _invertSelectionCommand ??= new RelayCommand(InvertDrainSelection);

        private RelayCommand _runCommand;
        public RelayCommand RunCommand => _runCommand ??= new RelayCommand(
            RunAutoSlope, () => !IsRunning && !IsComplete && AllDrains.Any(d => d.IsSelected));

        private RelayCommand _browseFolderCommand;
        public RelayCommand BrowseFolderCommand => _browseFolderCommand ??= new RelayCommand(BrowseForFolder);

        private RelayCommand _exportResultsCommand;
        public RelayCommand ExportResultsCommand => _exportResultsCommand ??= new RelayCommand(
            ExportResults, () => IsComplete && _lastResult?.Success == true);

        private RelayCommand _clearLogCommand;
        public RelayCommand ClearLogCommand => _clearLogCommand ??= new RelayCommand(ClearLog);

        // ── Constructor ───────────────────────────────────────────────────────
        public AutoSlopeDrainViewModel(UIDocument uidoc, UIApplication app, RoofData roofData)
        {
            UIDoc = uidoc;
            App = app;
            _roofData = roofData;

            RoofSubtitle = $"Revit 2026 · Roof: {roofData.Roof.Name} (Id {roofData.Roof.Id.Value})";

            var detectionService = new DrainDetectionService();

            foreach (var drain in roofData.DetectedDrains)
            {
                AllDrains.Add(drain);
                drain.PropertyChanged += OnDrainPropertyChanged;
            }

            foreach (var category in detectionService.GenerateSizeCategories(roofData.DetectedDrains))
                SizeFilters.Add(category);

            FilteredDrainsView = CollectionViewSource.GetDefaultView(AllDrains);
            FilteredDrainsView.Filter = FilterDrainItem;

            ExportFolderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                AppConstants.DefaultExportFolder);
            if (!Directory.Exists(ExportFolderPath))
                Directory.CreateDirectory(ExportFolderPath);

            AddLog(new LogEntry(LogLevel.Info, $"Detected {roofData.DetectedDrains.Count} drain opening(s)."));
            AddLog(new LogEntry(LogLevel.Info, $"Default export folder: {ExportFolderPath}"));

            UpdateSelectedCount();

            AutoSlopeDrainEventManager.Init();
        }

        // ── Filtering (core detection logic unchanged — reused via DrainDetectionService) ──
        private void OnDrainPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DrainItem.IsSelected)) return;
            UpdateSelectedCount();
            RunCommand.NotifyCanExecuteChanged();
        }

        private bool FilterDrainItem(object obj)
        {
            if (!(obj is DrainItem drain)) return false;
            if (SelectedSizeFilter == "All") return true;

            var svc = new DrainDetectionService();
            return svc.FilterDrainsBySize(new List<DrainItem> { drain }, SelectedSizeFilter).Any();
        }

        private void SelectAllDrains()
        {
            foreach (var d in AllDrains) d.IsSelected = true;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            AddLog(new LogEntry(LogLevel.Info, $"All {AllDrains.Count} drains selected."));
            RunCommand.NotifyCanExecuteChanged();
        }

        private void SelectNoneDrains()
        {
            foreach (var d in AllDrains) d.IsSelected = false;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            AddLog(new LogEntry(LogLevel.Info, "All drains deselected."));
            RunCommand.NotifyCanExecuteChanged();
        }

        private void InvertDrainSelection()
        {
            foreach (var d in AllDrains) d.IsSelected = !d.IsSelected;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            AddLog(new LogEntry(LogLevel.Info, $"Selection inverted: {AllDrains.Count(d => d.IsSelected)} selected."));
            RunCommand.NotifyCanExecuteChanged();
        }

        private void UpdateSelectedCount()
        {
            SelectedDrainsCount = FilteredDrainsView.Cast<DrainItem>().Count(d => d.IsSelected);
        }

        // ── Run ───────────────────────────────────────────────────────────────
        private void RunAutoSlope()
        {
            if (IsRunning || IsComplete) return;

            if (!double.TryParse(SlopeInput, out double slopePercent) || slopePercent <= 0)
            {
                AddLog(new LogEntry(LogLevel.Error, "Please enter a valid positive slope percentage."));
                return;
            }
            if (!double.TryParse(ConnectionThresholdInput, out double thresholdM) || thresholdM <= 0)
            {
                AddLog(new LogEntry(LogLevel.Error, "Please enter a valid positive connection threshold (m)."));
                return;
            }
            if (!int.TryParse(PathSampleCountInput, out int pathSamples) || pathSamples < 2)
            {
                AddLog(new LogEntry(LogLevel.Error, "Path sample count must be a whole number of 2 or more."));
                return;
            }

            var selectedSignatures = new List<DrainSelectionSignature>();
            foreach (var drain in AllDrains)
            {
                if (!drain.IsSelected) continue;
                selectedSignatures.Add(new DrainSelectionSignature
                {
                    CenterX = drain.CenterPoint.X,
                    CenterY = drain.CenterPoint.Y,
                    CenterZ = drain.CenterPoint.Z,
                    Width = drain.Width,
                    Height = drain.Height
                });
            }

            if (selectedSignatures.Count == 0)
            {
                AddLog(new LogEntry(LogLevel.Warning, "No drains selected for slope application."));
                return;
            }

            State = RunState.Running;
            LogEntries.Clear();
            AddLog(new LogEntry(LogLevel.Info, "Starting AutoSlope By Drain..."));

            if (ExportToCsv && !Directory.Exists(ExportFolderPath))
            {
                try { Directory.CreateDirectory(ExportFolderPath); }
                catch (Exception ex)
                {
                    AddLog(new LogEntry(LogLevel.Warning, $"Failed to create export directory: {ex.Message}"));
                }
            }

            AutoSlopeDrainHandler.Payload = new AutoSlopeDrainPayload
            {
                RoofId = RoofId,
                SelectedDrainSignatures = selectedSignatures,
                ExpectedDrainCount = AllDrains.Count,
                SlopePercent = slopePercent,
                ConnectionThresholdMeters = thresholdM,
                PathSampleCount = pathSamples,
                ProjectTitle = UIDoc?.Document?.Title ?? "Unknown Project",
                Log = AddLog,
                ExportConfig = new ExportConfig
                {
                    ExportPath = ExportFolderPath,
                    ExportToCsv = ExportToCsv
                },
                OnCompleted = result =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!result.Success)
                        {
                            AddLog(new LogEntry(LogLevel.Error, $"Run failed: {result.ErrorMessage}"));
                            State = RunState.Ready;
                            return;
                        }

                        _lastResult = result;
                        LongestPath_m = result.LongestPath_m;
                        HighestElevation_mm = result.HighestElevation_mm;
                        RunDuration_sec = result.RunDuration_sec;
                        FinalDrainCount = result.DrainCount;

                        State = RunState.Done;
                        ExportResultsCommand.NotifyCanExecuteChanged();

                        if (!string.IsNullOrEmpty(result.ExportedDetailedFilePath) ||
                            !string.IsNullOrEmpty(result.ExportedSummaryFilePath))
                        {
                            var answer = MessageBox.Show(
                                "CSV export completed. Open the export folder now?",
                                "Export Complete",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (answer == MessageBoxResult.Yes)
                                System.Diagnostics.Process.Start("explorer.exe", ExportFolderPath);
                        }
                    }));
                }
            };

            AutoSlopeDrainEventManager.Event.Raise();
        }

        // ── Export (re-export on demand) ──────────────────────────────────────
        private void ExportResults()
        {
            if (_lastResult == null || !_lastResult.Success)
            {
                AddLog(new LogEntry(LogLevel.Warning, "Run AutoSlope successfully first."));
                return;
            }

            AddLog(new LogEntry(LogLevel.Info,
                $"Last export: {_lastResult.ExportedDetailedFilePath ?? "(none)"}"));

            if (!string.IsNullOrEmpty(ExportFolderPath) && Directory.Exists(ExportFolderPath))
                System.Diagnostics.Process.Start("explorer.exe", ExportFolderPath);
        }

        private void BrowseForFolder()
        {
            var selected = DialogService.SelectFolder(ExportFolderPath);
            if (!string.IsNullOrEmpty(selected))
            {
                ExportFolderPath = selected;
                AddLog(new LogEntry(LogLevel.Info, $"Export folder set to: {ExportFolderPath}"));
            }
        }

        private void ClearLog()
        {
            LogEntries.Clear();
            AddLog(new LogEntry(LogLevel.Info, "Log cleared."));
        }

        // ── AddLog (thread-safe) ──────────────────────────────────────────────
        private void AddLog(LogEntry entry)
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
                LogEntries.Add(entry);
            else
                dispatcher.BeginInvoke(new Action(() => LogEntries.Add(entry)));
        }
    }
}
