using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.AutomatedSectionPlacer.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.AutomatedSectionPlacer.V001.Models;

namespace Revit26_Plugin.AutomatedSectionPlacer.V001.ViewModels
{
    /// <summary>
    /// Single shared ViewModel driving all 5 accordion stages in one window:
    /// 1 Select Views, 2 Preview Placement (packing/gap inputs + to-scale
    /// sheet canvas), 3 Suggested Placement (badges + editable grid), 4 Confirm
    /// &amp; Place, 5 Placement Complete. Holds state directly as properties (no
    /// PlacementSession object needed, since this is one Window / one ViewModel
    /// rather than chained Windows).
    ///
    /// V213: split into partial class files by stage for maintainability
    /// (was a single 732-line file in V213):
    ///   - AutomatedSectionPlacerViewModel.cs           this file — ctor, shared
    ///                                                   state, handler-completion
    ///                                                   dispatcher, cross-cutting commands
    ///   - AutomatedSectionPlacerViewModel.Stage1.cs     Select Views
    ///   - AutomatedSectionPlacerViewModel.Stage2.cs     Preview Placement / packing
    ///   - AutomatedSectionPlacerViewModel.Stage3.cs     Suggested Placement
    ///   - AutomatedSectionPlacerViewModel.Stage4.cs     Confirm &amp; Place
    ///   - AutomatedSectionPlacerViewModel.Stage5.cs     Placement Complete + log export
    ///   - AutomatedSectionPlacerViewModel.Settings.cs   settings load/save
    /// </summary>
    public partial class AutomatedSectionPlacerViewModel : ObservableObject, System.IDisposable
    {
        private const string ToolName = "AutomatedSectionPlacer";

        private readonly Document _doc;
        private readonly ExternalEvent _event;
        private readonly AutomatedSectionPlacerHandler _handler;
        private readonly Dispatcher _dispatcher;
        private readonly ElementId _planViewId;
        private AutomatedSectionPlacerSettings _settings = new();

        /// <summary>Name of the Plan View this tool was launched from — shown in the Stage 1 header ("N section views detected on '<PlanViewName>'").</summary>
        [ObservableProperty] private string _planViewName = string.Empty;

        public ObservableCollection<ViewInfo> AllViews { get; } = new();
        public ICollectionView ViewsView { get; }
        public ObservableCollection<ViewTypeFilterOption> ViewTypeFilters { get; } = new();
        public ObservableCollection<TitleblockOption> Titleblocks { get; } = new();
        public ObservableCollection<SheetGroup> SuggestedSheets { get; } = new();
        public ObservableCollection<ViewPlacement> AllPlacements { get; } = new();
        public ObservableCollection<LogEntry> Logs { get; } = new();
        /// <summary>Every existing ViewSheet in the project — offered in each suggested sheet's "Target Sheet" dropdown.</summary>
        public ObservableCollection<ExistingSheetOption> ExistingSheets { get; } = new();

        // ---- Accordion expand/collapse (all freely toggle-able) ----
        [ObservableProperty] private bool _stage1Expanded = true;
        [ObservableProperty] private bool _stage2Expanded;
        [ObservableProperty] private bool _stage3Expanded;
        [ObservableProperty] private bool _stage4Expanded;
        [ObservableProperty] private bool _stage5Expanded;

        // ---- Busy state ----
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _busyMessage = string.Empty;

        public AutomatedSectionPlacerViewModel(UIDocument uiDoc, ElementId planViewId)
        {
            _doc = uiDoc.Document;
            _planViewId = planViewId;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _handler = new AutomatedSectionPlacerHandler(uiDoc);
            _handler.RequestCompleted += OnHandlerRequestCompleted;
            _handler.ProgressChanged += OnHandlerProgress;
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
        /// (Close button, Esc key). No longer fires after Open Selected — that
        /// command leaves the window open now (V213).</summary>
        public event Action? CloseRequested;

        /// <summary>
        /// Called from the View's Window.Closing handler (V213 — new). Ensures
        /// settings are persisted even if the user closes the window without
        /// ever completing Stage 1 (previously settings only saved from
        /// NextToStage2, so a same-session close before that point silently
        /// lost any margin/titleblock changes).
        /// </summary>
        public void OnWindowClosing() => SaveSettings();

        /// <summary>
        /// V222 FIX: this ViewModel owns _handler and subscribed to its
        /// RequestCompleted event, but never unsubscribed or disposed the
        /// ExternalEvent — the same leak shape already fixed in
        /// RefSectionHeadPlacer_V013 and SheetAutoRearrange_V025. Without
        /// this, _handler stays subscribed to this VM instance after the
        /// window closes, keeping the whole VM alive for the rest of the
        /// Revit session. Called from the window's Closing handler
        /// alongside OnWindowClosing/SaveSettings.
        /// </summary>
        public void Dispose()
        {
            _handler.RequestCompleted -= OnHandlerRequestCompleted;
            _handler.ProgressChanged -= OnHandlerProgress;
            _event?.Dispose();
        }

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
                    case AutomatedSectionPlacerRequest.LoadViews:
                        HandleLoadViewsCompleted();
                        break;
                    case AutomatedSectionPlacerRequest.PlaceViews:
                        HandlePlaceViewsCompleted();
                        break;
                    case AutomatedSectionPlacerRequest.OpenSheets:
                        // V213 UPDATE: previously auto-closed the window here once
                        // OpenSheets finished. Confirmed with Rafi: "Open Selected"
                        // now opens sheets but leaves the window open — "Close" is
                        // the only way to dismiss it. Just log completion instead.
                        Logs.Add(new LogEntry(LogLevel.Info, "Selected sheets opened."));
                        break;
                }

                NextToStage2Command.NotifyCanExecuteChanged();
                PlaceViewsCommand.NotifyCanExecuteChanged();
            });
        }
    }
}
