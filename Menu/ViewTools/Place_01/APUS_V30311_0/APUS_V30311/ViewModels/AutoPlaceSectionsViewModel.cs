using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.APUS_V311.ExternalEvents;
using Revit26_Plugin.APUS_V311.Models;
using Revit26_Plugin.APUS_V311.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V311.ViewModels
{
    /// <summary>
    /// Main ViewModel for Auto Place Sections (APUS).
    /// Contains UI state only. All Revit API logic
    /// is executed via ExternalEvent.
    /// </summary>
    public class AutoPlaceSectionsViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uidoc;

        // ===================== DATA =====================

        public ObservableCollection<SectionItemViewModel> Sections { get; }
            = new ObservableCollection<SectionItemViewModel>();

        /// <summary>
        /// DataGrid binds to this collection view.
        /// </summary>
        public ICollectionView FilteredSections { get; }

        public ObservableCollection<TitleBlockItemViewModel> TitleBlocks { get; }
            = new ObservableCollection<TitleBlockItemViewModel>();

        public ObservableCollection<LogEntryViewModel> LogEntries { get; }
            = new ObservableCollection<LogEntryViewModel>();

        public PlacementProgressViewModel Progress { get; }
            = new PlacementProgressViewModel();

        // ===================== FILTER SOURCES =====================

        public ObservableCollection<string> PlacementScopes { get; }
            = new ObservableCollection<string>();

        public ObservableCollection<string> SheetNumbers { get; }
            = new ObservableCollection<string>();

        public ObservableCollection<string> PlacementStates { get; }
            = new ObservableCollection<string>
            {
                "Unplaced Only",
                "Placed Only",
                "Both"
            };

        // ===================== FILTER VALUES =====================

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

        // Default = Both (ensures section names are visible)
        private string _selectedPlacementState = "Both";
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

        // ===================== TITLE BLOCK =====================

        private TitleBlockItemViewModel _selectedTitleBlock;
        public TitleBlockItemViewModel SelectedTitleBlock
        {
            get => _selectedTitleBlock;
            set
            {
                if (_selectedTitleBlock == value) return;
                _selectedTitleBlock = value;
                OnPropertyChanged();

                if (value != null)
                    LogInfo($"Title Block selected: {value.DisplayName}");
            }
        }

        // ===================== LAYOUT INPUTS (mm) =====================

        public double LeftMarginMm { get; set; } = 40;
        public double RightMarginMm { get; set; } = 150;
        public double TopMarginMm { get; set; } = 40;
        public double BottomMarginMm { get; set; } = 100;

        public double HorizontalGapMm { get; set; } = 10;
        public double VerticalGapMm { get; set; } = 10;
        public double YToleranceMm { get; set; } = 10;

        // ===================== COMMANDS =====================

        public IRelayCommand PlaceCommand { get; }
        public IRelayCommand CancelCommand { get; }

        // ===================== CONSTRUCTOR =====================

        public AutoPlaceSectionsViewModel(UIDocument uidoc)
        {
            _uidoc = uidoc;

            FilteredSections = CollectionViewSource.GetDefaultView(Sections);
            FilteredSections.Filter = FilterPredicate;

            PlaceCommand = new RelayCommand(ExecutePlacement);
            CancelCommand = new RelayCommand(ExecuteCancel);

            CollectSections();
            CollectTitleBlocks();
        }

        // ===================== COLLECTION =====================

        private void CollectSections()
        {
            LogInfo("Collecting section views…");

            var service = new SectionCollectionService(_uidoc.Document);
            var items = service.Collect();

            Sections.Clear();
            PlacementScopes.Clear();
            SheetNumbers.Clear();

            foreach (var item in items)
            {
                Sections.Add(item);

                if (!string.IsNullOrWhiteSpace(item.PlacementScope) &&
                    !PlacementScopes.Contains(item.PlacementScope))
                {
                    PlacementScopes.Add(item.PlacementScope);
                }

                if (item.IsPlaced &&
                    !string.IsNullOrWhiteSpace(item.SheetNumber) &&
                    !SheetNumbers.Contains(item.SheetNumber))
                {
                    SheetNumbers.Add(item.SheetNumber);
                }
            }

            // Sort sheet numbers
            var ordered = SheetNumbers.OrderBy(x => x).ToList();
            SheetNumbers.Clear();
            foreach (var sn in ordered)
                SheetNumbers.Add(sn);

            FilteredSections.Refresh();
            LogInfo($"{Sections.Count} section(s) collected.");
        }

        private void CollectTitleBlocks()
        {
            LogInfo("Collecting title blocks…");

            var service = new TitleBlockCollectionService(_uidoc.Document);
            var items = service.Collect();

            TitleBlocks.Clear();
            foreach (var tb in items)
                TitleBlocks.Add(tb);

            SelectedTitleBlock = TitleBlocks.FirstOrDefault();

            LogInfo($"{TitleBlocks.Count} title block(s) found.");
        }

        // ===================== FILTER =====================

        private bool FilterPredicate(object obj)
        {
            if (obj is not SectionItemViewModel item)
                return false;

            if (SelectedPlacementState == "Placed Only" && !item.IsPlaced)
                return false;

            if (SelectedPlacementState == "Unplaced Only" && item.IsPlaced)
                return false;

            if (!string.IsNullOrWhiteSpace(SelectedPlacementScope))
            {
                if (string.IsNullOrWhiteSpace(item.PlacementScope) ||
                    !item.PlacementScope.Contains(
                        SelectedPlacementScope,
                        StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(SheetNumberFilter))
            {
                if (!item.IsPlaced ||
                    string.IsNullOrWhiteSpace(item.SheetNumber) ||
                    item.SheetNumber != SheetNumberFilter)
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

            if (SelectedTitleBlock == null)
            {
                LogWarning("No title block selected.");
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

        // ===================== UI REFRESH (CRITICAL) =====================

        /// <summary>
        /// Called by ExternalEvent AFTER model changes.
        /// Must always execute on UI thread.
        /// </summary>
        public void RequestUiRefresh()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ReloadFromModel();
            }));
        }


        private void ReloadFromModel()
        {
            LogInfo("Refreshing UI from model…");

            CollectSections();
            FilteredSections.Refresh();

            LogInfo("UI refresh complete.");
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
