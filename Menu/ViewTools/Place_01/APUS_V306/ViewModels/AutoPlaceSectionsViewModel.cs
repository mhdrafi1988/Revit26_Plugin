using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.APUS_V306.ExternalEvents;
using Revit26_Plugin.APUS_V306.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V306.ViewModels
{
    /// <summary>
    /// Main ViewModel for Auto Place Sections (APUS).
    /// UI state only — Revit API logic runs inside ExternalEvent handler.
    /// </summary>
    public class AutoPlaceSectionsViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uidoc;

        // ===================== MASTER DATA =====================

        /// <summary>
        /// Full, unfiltered collection of sections.
        /// </summary>
        public ObservableCollection<SectionItemViewModel> Sections { get; }
            = new ObservableCollection<SectionItemViewModel>();

        /// <summary>
        /// Filtered view bound to the DataGrid.
        /// </summary>
        public ICollectionView FilteredSections { get; }

        public ObservableCollection<LogEntryViewModel> LogEntries { get; }
            = new ObservableCollection<LogEntryViewModel>();

        public PlacementProgressViewModel Progress { get; }
            = new PlacementProgressViewModel();

        // ===================== FILTER OPTIONS =====================

        public ObservableCollection<string> PlacementScopes { get; }
            = new ObservableCollection<string>();

        public ObservableCollection<string> PlacementStates { get; }
            = new ObservableCollection<string>
            {
                "Unplaced Only",
                "Placed Only",
                "Both"
            };

        private string _selectedPlacementScope;
        public string SelectedPlacementScope
        {
            get => _selectedPlacementScope;
            set
            {
                if (_selectedPlacementScope == value) return;
                _selectedPlacementScope = value;
                OnPropertyChanged();
                FilteredSections.Refresh();
            }
        }

        private string _selectedPlacementState = "Unplaced Only";
        public string SelectedPlacementState
        {
            get => _selectedPlacementState;
            set
            {
                if (_selectedPlacementState == value) return;
                _selectedPlacementState = value;
                OnPropertyChanged();
                FilteredSections.Refresh();
            }
        }

        private string _sheetNumberFilter;
        public string SheetNumberFilter
        {
            get => _sheetNumberFilter;
            set
            {
                if (_sheetNumberFilter == value) return;
                _sheetNumberFilter = value;
                OnPropertyChanged();
                FilteredSections.Refresh();
            }
        }

        // ===================== LAYOUT INPUTS (mm) =====================

        public double LeftMarginMm { get; set; } = 20;
        public double RightMarginMm { get; set; } = 20;
        public double TopMarginMm { get; set; } = 20;
        public double BottomMarginMm { get; set; } = 20;

        public double HorizontalGapMm { get; set; } = 20;
        public double VerticalGapMm { get; set; } = 20;

        public double YToleranceMm { get; set; } = 50;

        // ===================== COMMANDS =====================

        public IRelayCommand PlaceCommand { get; }
        public IRelayCommand CancelCommand { get; }

        // ===================== CONSTRUCTOR =====================

        public AutoPlaceSectionsViewModel(UIDocument uidoc)
        {
            _uidoc = uidoc;

            // Setup filtered collection view
            FilteredSections = CollectionViewSource.GetDefaultView(Sections);
            FilteredSections.Filter = FilterPredicate;

            PlaceCommand = new RelayCommand(ExecutePlacement);
            CancelCommand = new RelayCommand(ExecuteCancel);

            CollectSections();
        }

        // ===================== COLLECTION =====================

        private void CollectSections()
        {
            LogInfo("Collecting section views…");

            var service = new SectionCollectionService(_uidoc.Document);
            var items = service.Collect();

            Sections.Clear();
            PlacementScopes.Clear();

            foreach (var item in items)
            {
                Sections.Add(item);

                if (!string.IsNullOrWhiteSpace(item.PlacementScope) &&
                    !PlacementScopes.Contains(item.PlacementScope))
                {
                    PlacementScopes.Add(item.PlacementScope);
                }
            }

            FilteredSections.Refresh();
            LogInfo($"{Sections.Count} section(s) collected.");
        }

        // ===================== UI FILTER LOGIC =====================

        private bool FilterPredicate(object obj)
        {
            if (obj is not SectionItemViewModel item)
                return false;

            // Placement State
            if (SelectedPlacementState == "Placed Only" && !item.IsPlaced)
                return false;

            if (SelectedPlacementState == "Unplaced Only" && item.IsPlaced)
                return false;

            // Placement Scope
            if (!string.IsNullOrWhiteSpace(SelectedPlacementScope))
            {
                if (string.IsNullOrWhiteSpace(item.PlacementScope) ||
                    !item.PlacementScope.Contains(
                        SelectedPlacementScope,
                        StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Sheet Number filter (placed views only)
            if (!string.IsNullOrWhiteSpace(SheetNumberFilter))
            {
                if (!item.IsPlaced ||
                    string.IsNullOrWhiteSpace(item.SheetNumber) ||
                    !item.SheetNumber.Contains(
                        SheetNumberFilter,
                        StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        // ===================== COMMAND EXECUTION =====================

        private void ExecutePlacement()
        {
            if (!Sections.Any(x => x.IsSelected))
            {
                LogWarning("No sections selected.");
                return;
            }

            AutoPlaceSectionsEventManager.Handler.ViewModel = this;
            AutoPlaceSectionsEventManager.ExternalEvent.Raise();
        }

        private void ExecuteCancel()
        {
            Progress.Cancel();
            LogWarning("Cancel requested.");
        }

        // ===================== LOGGING =====================

        public void LogInfo(string message) => AddLog(LogLevel.Info, message);
        public void LogWarning(string message) => AddLog(LogLevel.Warning, message);
        public void LogError(string message) => AddLog(LogLevel.Error, message);

        private void AddLog(LogLevel level, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogEntries.Add(new LogEntryViewModel(level, message));
            });
        }

        // ===================== INotifyPropertyChanged =====================

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
