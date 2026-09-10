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

namespace Revit26_Plugin.CombinedRoofTools.V001.UI.ViewModels
{
    public partial class CombinedRoofToolsViewModel : ObservableObject
    {
        private readonly UIApplication _app;

        [ObservableProperty]
        private string roofLabel;

        [ObservableProperty]
        private bool isChangingRoof;

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

        [RelayCommand]
        private void ChangeRoof()
        {
            if (IsChangingRoof) return;
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
    }
}
