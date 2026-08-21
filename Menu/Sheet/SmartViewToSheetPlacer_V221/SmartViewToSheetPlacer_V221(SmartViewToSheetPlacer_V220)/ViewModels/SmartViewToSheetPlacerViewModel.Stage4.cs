using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V221.Infrastructure.ExternalEvents;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V221.ViewModels
{
    /// <summary>Stage 4: Confirm &amp; Place — the one Revit-API-touching action
    /// besides LoadViews/OpenSheets. Raises the shared ExternalEvent to create
    /// sheets and place viewports inside a single Transaction (see Handler).</summary>
    public partial class SmartViewToSheetPlacerViewModel
    {
        // ---- Stage 4 state ----
        [ObservableProperty] private bool _stage4Complete;
        public string Stage4StatusLabel => Stage4Complete ? "Complete" : (Stage3Complete ? "In Progress" : "Not Started");

        [RelayCommand]
        private void BackToStage3()
        {
            Stage4Expanded = false;
            Stage3Expanded = true;
        }

        [RelayCommand(CanExecute = nameof(CanPlaceViews))]
        private void PlaceViews()
        {
            if (SelectedTitleblock == null) return;

            IsBusy = true;
            BusyMessage = "Creating sheets and placing views...";

            _handler.TitleblockFamilySymbolId = SelectedTitleblock.FamilySymbolId;
            _handler.MarginTopMm = MarginTopMm;
            _handler.MarginBottomMm = MarginBottomMm;
            _handler.MarginLeftMm = MarginLeftMm;
            _handler.MarginRightMm = MarginRightMm;
            _handler.SheetsToPlace = SuggestedSheets.ToList();
            _handler.Request = SmartViewToSheetPlacerRequest.PlaceViews;
            _event.Raise();
        }

        private bool CanPlaceViews() => !IsBusy && SuggestedSheets.Count > 0;

        partial void OnStage4CompleteChanged(bool value)
        {
            OnPropertyChanged(nameof(Stage4StatusLabel));
            OnPropertyChanged(nameof(Stage5StatusLabel));
        }

        /// <summary>
        /// Populates Stage 5 results from the handler's PlaceViews output and
        /// advances the accordion. Lives here (not Stage5 partial) because it's
        /// the direct continuation of this stage's PlaceViews action.
        /// </summary>
        private void HandlePlaceViewsCompleted()
        {
            if (!_handler.LastRunSucceeded)
            {
                Logs.Add(new LogEntry(LogLevel.Error, "Placement did not complete successfully. See log above."));
                return;
            }

            PlacedSheetCount = SuggestedSheets.Count(s => s.CreatedSheetId != null);
            PlacedViewCount = _handler.PlacedCount;
            FailedCount = _handler.FailedCount;

            Stage4Complete = true;
            Stage4Expanded = false;
            Stage5Expanded = true;

            AutoSaveLogs();
        }
    }
}
