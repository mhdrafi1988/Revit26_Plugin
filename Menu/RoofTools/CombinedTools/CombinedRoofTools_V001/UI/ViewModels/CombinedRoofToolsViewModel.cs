// =======================================================
// File: CombinedRoofToolsViewModel.cs
// Location: UI/ViewModels/
// Hosts the 5 existing tool ViewModels (unmodified — same classes each
// tool's own standalone window binds to) under one shared roof
// selection. "Change Roof" re-picks and rebuilds all 5 via
// CombinedRoofToolsInitializer/ChangeRoofEventManager, replacing each
// child ViewModel wholesale — every tab's DataContext binding
// (set declaratively in CombinedRoofToolsWindow.xaml) picks up the new
// instance automatically through property-changed notification.
// =======================================================

using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoSlopeByDrain.V007.UI.ViewModels;
using Revit26_Plugin.CombinedRoofTools.V001.Core;
using Revit26_Plugin.CombinedRoofTools.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.CreaserAdv.V009.ViewModels;
using Revit26_Plugin.InnerLoopDivider.V009.UI.ViewModels;
using Revit26_Plugin.InnerLoopsAndPerpendicular.V005.UI.ViewModels;
using Revit26_Plugin.OuterCurveDivider.V004.UI.ViewModels;
using System;
using System.Threading.Tasks;

namespace Revit26_Plugin.CombinedRoofTools.V001.UI.ViewModels
{
    public partial class CombinedRoofToolsViewModel : ObservableObject
    {
        // Tab order matches CombinedRoofToolsWindow.xaml's TabControl and is
        // reused both for the default launch tab and for RunAll's live
        // tab-switching as it works through the pipeline.
        private const int InnerLoopDividerTabIndex = 0;
        private const int InnerLoopsAndPerpendicularTabIndex = 1;
        private const int OuterCurveDividerTabIndex = 2;
        private const int AutoSlopeByDrainTabIndex = 3;
        private const int CreaserAdvTabIndex = 4;

        private readonly UIApplication _app;

        [ObservableProperty]
        private string roofLabel;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        [NotifyCanExecuteChangedFor(nameof(RunAllCommand))]
        [NotifyCanExecuteChangedFor(nameof(ChangeRoofCommand))]
        private bool isChangingRoof;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        [NotifyCanExecuteChangedFor(nameof(RunAllCommand))]
        [NotifyCanExecuteChangedFor(nameof(ChangeRoofCommand))]
        private bool isRunningAll;

        /// <summary>True while either Change Roof or Run All is in flight — both
        /// drive Revit-side ExternalEvents, so neither may overlap the other.</summary>
        public bool IsBusy => IsChangingRoof || IsRunningAll;

        /// <summary>Which tab is shown. Defaults to Auto Slope By Drain (the
        /// suite's most-used tool) as the window's launch view; RunAll also
        /// drives this live so the active tab tracks whichever tool it's
        /// currently running.</summary>
        [ObservableProperty]
        private int activeTabIndex = AutoSlopeByDrainTabIndex;

        /// <summary>Status text shown next to the Run All button while it's running.</summary>
        [ObservableProperty]
        private string runAllStatusMessage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsInnerLoopDividerAvailable))]
        private InnerLoopDividerViewModel innerLoopDividerVm;
        [ObservableProperty]
        private string innerLoopDividerUnavailableReason;
        public bool IsInnerLoopDividerAvailable => InnerLoopDividerVm != null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsInnerLoopsAndPerpendicularAvailable))]
        private InnerLoopsAndPerpendicularViewModel innerLoopsAndPerpendicularVm;
        [ObservableProperty]
        private string innerLoopsAndPerpendicularUnavailableReason;
        public bool IsInnerLoopsAndPerpendicularAvailable => InnerLoopsAndPerpendicularVm != null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOuterCurveDividerAvailable))]
        private CurveDividerViewModel outerCurveDividerVm;
        [ObservableProperty]
        private string outerCurveDividerUnavailableReason;
        public bool IsOuterCurveDividerAvailable => OuterCurveDividerVm != null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAutoSlopeByDrainAvailable))]
        private AutoSlopeDrainViewModel autoSlopeByDrainVm;
        [ObservableProperty]
        private string autoSlopeByDrainUnavailableReason;
        public bool IsAutoSlopeByDrainAvailable => AutoSlopeByDrainVm != null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCreaserAdvAvailable))]
        private CreaserAdvViewModel creaserAdvVm;
        [ObservableProperty]
        private string creaserAdvUnavailableReason;
        public bool IsCreaserAdvAvailable => CreaserAdvVm != null;

        public CombinedRoofToolsViewModel(UIApplication app, CombinedRoofToolsInitResult initial)
        {
            _app = app;
            ChangeRoofEventManager.Init();
            Apply(initial);
        }

        private void Apply(CombinedRoofToolsInitResult r)
        {
            RoofLabel = r.RoofLabel;

            InnerLoopDividerVm = r.InnerLoopDividerVm;
            InnerLoopDividerUnavailableReason = r.InnerLoopDividerUnavailableReason;

            InnerLoopsAndPerpendicularVm = r.InnerLoopsAndPerpendicularVm;
            InnerLoopsAndPerpendicularUnavailableReason = r.InnerLoopsAndPerpendicularUnavailableReason;

            OuterCurveDividerVm = r.OuterCurveDividerVm;
            OuterCurveDividerUnavailableReason = r.OuterCurveDividerUnavailableReason;

            AutoSlopeByDrainVm = r.AutoSlopeByDrainVm;
            AutoSlopeByDrainUnavailableReason = r.AutoSlopeByDrainUnavailableReason;

            CreaserAdvVm = r.CreaserAdvVm;
            CreaserAdvUnavailableReason = r.CreaserAdvUnavailableReason;
        }

        /// <summary>Called by the window's Closing handler so settings persisted
        /// by individual tools (currently just Auto Slope By Drain) are saved
        /// even if the user closes without clicking that tool's own actions.</summary>
        public void SaveOnClose()
        {
            AutoSlopeByDrainVm?.SaveSettingsOnClose();
        }

        [RelayCommand(CanExecute = nameof(CanChangeRoof))]
        private void ChangeRoof()
        {
            if (IsBusy) return;
            IsChangingRoof = true;

            ChangeRoofHandler.Payload = new Infrastructure.ExternalEvents.ChangeRoofPayload
            {
                OnCompleted = result =>
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        AutoSlopeByDrainVm?.SaveSettingsOnClose();
                        Apply(result);
                        IsChangingRoof = false;
                    }));
                },
                OnCancelled = () =>
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        IsChangingRoof = false;
                    }));
                },
                OnFailed = _ =>
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        IsChangingRoof = false;
                    }));
                }
            };

            ChangeRoofEventManager.Event.Raise();
        }

        private bool CanChangeRoof() => !IsBusy;

        // ── Run All ──────────────────────────────────────────────────────────
        // Runs all 5 tools back-to-back for the current roof, in the pipeline
        // order that makes geometric sense: points get added to the roof first
        // (Inner Loop Divider → Inner Loops + Perpendicular → Outer Curve
        // Divider), then Auto Slope By Drain sets elevations using every added
        // point, and finally Creaser Adv places ridge/valley lines against the
        // finished, sloped surface. Any tab a tool leaves not fully configured
        // (nothing selected, no prior "Generate" pass) is auto-filled with
        // sensible defaults — select everything visible, run "Generate" before
        // "Apply" where that two-step flow exists — so one click can carry a
        // fresh window all the way through with no manual per-tab setup.
        // Tools unavailable for this roof (null VM) are skipped.
        //
        // Each tool's own Run/Apply command fires its own ExternalEvent and
        // reports back later via that tool's *Completed event (added
        // alongside each tool's existing OnCompleted callback) — RunAll awaits
        // each one before moving to the next so steps never overlap.

        private bool CanRunAll() => !IsBusy;

        [RelayCommand(CanExecute = nameof(CanRunAll))]
        private async Task RunAll()
        {
            if (IsBusy) return;
            IsRunningAll = true;

            try
            {
                await RunInnerLoopDividerStep();
                await RunInnerLoopsAndPerpendicularStep();
                await RunOuterCurveDividerStep();
                await RunAutoSlopeByDrainStep();
                await RunCreaserAdvStep();
            }
            finally
            {
                RunAllStatusMessage = null;
                IsRunningAll = false;
            }
        }

        private async Task RunInnerLoopDividerStep()
        {
            var vm = InnerLoopDividerVm;
            if (vm == null) return;

            ActiveTabIndex = InnerLoopDividerTabIndex;
            RunAllStatusMessage = "Running All — Inner Loop Divider…";

            vm.SelectAllCommand.Execute(null);
            await ExecuteAndWaitAsync(vm.ApplyDivisionCommand, h => vm.ApplyDivisionCompleted += h, h => vm.ApplyDivisionCompleted -= h);
        }

        private async Task RunInnerLoopsAndPerpendicularStep()
        {
            var vm = InnerLoopsAndPerpendicularVm;
            if (vm == null) return;

            ActiveTabIndex = InnerLoopsAndPerpendicularTabIndex;
            RunAllStatusMessage = "Running All — Inner Loops + Perpendicular…";

            vm.SelectAllCommand.Execute(null);
            // Apply only has points to work with after Generate has run —
            // fired unconditionally here since Run All can't assume a
            // manual "Generate" click already happened on this tab.
            await ExecuteAndWaitAsync(vm.GeneratePerpendicularPointsCommand, h => vm.GeneratePerpendicularPointsCompleted += h, h => vm.GeneratePerpendicularPointsCompleted -= h);
            await ExecuteAndWaitAsync(vm.ApplyPerpendicularCommand, h => vm.ApplyPerpendicularCompleted += h, h => vm.ApplyPerpendicularCompleted -= h);
        }

        private async Task RunOuterCurveDividerStep()
        {
            var vm = OuterCurveDividerVm;
            if (vm == null) return;

            ActiveTabIndex = OuterCurveDividerTabIndex;
            RunAllStatusMessage = "Running All — Outer Curve Divider…";

            vm.SelectAllCommand.Execute(null);
            await ExecuteAndWaitAsync(vm.ApplyCommand, h => vm.ApplyCompleted += h, h => vm.ApplyCompleted -= h);
        }

        private async Task RunAutoSlopeByDrainStep()
        {
            var vm = AutoSlopeByDrainVm;
            if (vm == null) return;

            ActiveTabIndex = AutoSlopeByDrainTabIndex;
            RunAllStatusMessage = "Running All — Auto Slope By Drain…";

            vm.SelectAllCommand.Execute(null);
            await ExecuteAndWaitAsync(vm.RunAutoSlopeCommand, h => vm.RunCompleted += h, h => vm.RunCompleted -= h);
        }

        private async Task RunCreaserAdvStep()
        {
            var vm = CreaserAdvVm;
            if (vm == null) return;

            ActiveTabIndex = CreaserAdvTabIndex;
            RunAllStatusMessage = "Running All — Creaser Adv…";

            if (vm.SelectedDetailSymbol == null && vm.DetailSymbols.Count > 0)
                vm.SelectedDetailSymbol = vm.DetailSymbols[0];

            await ExecuteAndWaitAsync(vm.RunCommand, h => vm.RunCompleted += h, h => vm.RunCompleted -= h);
        }

        /// <summary>Executes a RelayCommand backed by an ExternalEvent and awaits
        /// its *Completed event before returning, so pipeline steps never
        /// overlap. Skipped (no-op) if the command's own prerequisites still
        /// aren't met even after RunAll's auto-fill (e.g. a tool found nothing
        /// at all on this roof) — CanExecute is the same gate its own button uses.</summary>
        private static async Task ExecuteAndWaitAsync(IRelayCommand command, Action<Action<bool>> subscribe, Action<Action<bool>> unsubscribe)
        {
            if (!command.CanExecute(null)) return;

            var tcs = new TaskCompletionSource<bool>();
            void OnCompleted(bool success) => tcs.TrySetResult(success);

            subscribe(OnCompleted);
            try
            {
                command.Execute(null);
                await tcs.Task;
            }
            finally
            {
                unsubscribe(OnCompleted);
            }
        }
    }
}
