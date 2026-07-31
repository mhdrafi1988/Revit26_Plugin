using System;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V204.Infrastructure.ExternalEvents;
using Revit26_Plugin.SmartViewToSheetPlacer.V204.Models;
using System.ComponentModel; // <-- Add this using directive

namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.ViewModels
{
    /// <summary>
    /// Single shared ViewModel driving all 5 accordion stages in one window:
    /// 1 Select Views, 2 Preview Placement (packing/gap inputs + to-scale
    /// sheet canvas), 3 Suggested Placement (badges + editable grid), 4 Confirm
    /// &amp; Place, 5 Placement Complete. Holds state directly as properties (no
    /// PlacementSession object needed, since this is one Window / one ViewModel
    /// rather than chained Windows).
    ///
    /// V204: split into partial class files by stage for maintainability
    /// (was a single 732-line file in V203):
    ///   - SmartViewToSheetPlacerViewModel.cs           this file — ctor, shared
    ///                                                   state, handler-completion
    ///                                                   dispatcher, cross-cutting commands
    ///   - SmartViewToSheetPlacerViewModel.Stage1.cs     Select Views
    ///   - SmartViewToSheetPlacerViewModel.Stage2.cs     Preview Placement / packing
    ///   - SmartViewToSheetPlacerViewModel.Stage3.cs     Suggested Placement
    ///   - SmartViewToSheetPlacerViewModel.Stage4.cs     Confirm &amp; Place
    ///   - SmartViewToSheetPlacerViewModel.Stage5.cs     Placement Complete + log export
    ///   - SmartViewToSheetPlacerViewModel.Settings.cs   settings load/save
    /// </summary>
    public partial class SmartViewToSheetPlacerViewModel : ObservableObject
    {
        private const string ToolName = "SmartViewToSheetPlacer";

        private readonly Document _doc;
        private readonly ExternalEvent _event;
        private readonly SmartViewToSheetPlacerHandler _handler;
        private readonly Dispatcher _dispatcher;
        private SmartViewToSheetPlacerSettings _settings = new();

        public ObservableCollection<ViewInfo> AllViews { get; } = new();
        public ICollectionView ViewsView { get; }
        public ObservableCollection<ViewTypeFilterOption> ViewTypeFilters { get; } = new();
        public ObservableCollection<TitleblockOption> Titleblocks { get; } = new();
        public ObservableCollection<SheetGroup> SuggestedSheets { get; } = new();
        public ObservableCollection<ViewPlacement> AllPlacements { get; } = new();
        public ObservableCollection<LogEntry> Logs { get; } = new();

        // ---- Accordion expand/collapse (all freely toggle-able) ----
        [ObservableProperty] private bool _stage1Expanded = true;
        [ObservableProperty] private bool _stage2Expanded;
        [ObservableProperty] private bool _stage3Expanded;
        [ObservableProperty] private bool _stage4Expanded;
        [ObservableProperty] private bool _stage5Expanded;

        // ---- Busy state ----
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _busyMessage = string.Empty;

        public SmartViewToSheetPlacerViewModel(UIDocument uiDoc)
        {
            _doc = uiDoc.Document;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _handler = new SmartViewToSheetPlacerHandler(uiDoc);
            _handler.RequestCompleted += OnHandlerRequestCompleted;
            _event = ExternalEvent.Create(_handler);

            ViewsView = CollectionViewSource.GetDefaultView(AllViews);
            ViewsView.Filter = FilterViews;

            LoadSettings();
            RunLoadViews();
        }

        // ─────────────────────────────────────────────────────────────
        // Cross-cutting: window close
        // ─────────────────────────────────────────────────────────────
        [RelayCommand]
        private void Close() => CloseRequested?.Invoke();

        /// <summary>Raised when the ViewModel wants the owning Window to close
        /// (Close/Cancel button, Esc key — V204 — or after OpenSelectedAndClose).</summary>
        public event Action? CloseRequested;

        /// <summary>
        /// Called from the View's Window.Closing handler (V204 — new). Ensures
        /// settings are persisted even if the user closes the window without
        /// ever completing Stage 1 (previously settings only saved from
        /// NextToStage2, so a same-session close before that point silently
        /// lost any margin/titleblock changes).
        /// </summary>
        public void OnWindowClosing() => SaveSettings();

        // ─────────────────────────────────────────────────────────────
        // Handler completion callback (always marshal back via Dispatcher,
        // since ExternalEvent callbacks may not be on the WPF UI thread
        // depending on Revit's internal scheduling). Individual Handle*Completed
        // methods live in each stage's partial file, matching what they populate.
        // ─────────────────────────────────────────────────────────────
        private void OnHandlerRequestCompleted()
        {
            _dispatcher.Invoke(() =>
            {
                IsBusy = false;
                BusyMessage = string.Empty;

                foreach (var log in _handler.Logs)
                    Logs.Add(log);
                _handler.Logs.Clear();

                switch (_handler.Request)
                {
                    case SmartViewToSheetPlacerRequest.LoadViews:
                        HandleLoadViewsCompleted();
                        break;
                    case SmartViewToSheetPlacerRequest.PlaceViews:
                        HandlePlaceViewsCompleted();
                        break;
                    case SmartViewToSheetPlacerRequest.OpenSheets:
                        CloseRequested?.Invoke();
                        break;
                }

                NextToStage2Command.NotifyCanExecuteChanged();
                PlaceViewsCommand.NotifyCanExecuteChanged();
            });
        }
    }
}
