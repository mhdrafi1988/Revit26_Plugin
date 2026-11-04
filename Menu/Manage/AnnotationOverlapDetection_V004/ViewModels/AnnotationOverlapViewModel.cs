using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Helpers;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Models;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.ViewModels
{
    /// <summary>
    /// View model for the Annotation Overlap Detection window.
    /// </summary>
    public partial class AnnotationOverlapViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly View _activeView;
        private readonly ExternalEvent _zoomEvent;
        private readonly ZoomToElementEventHandler _zoomHandler;
        private readonly ExternalEvent _refreshEvent;
        private readonly RefreshAnnotationsEventHandler _refreshHandler;
        private bool _hasRun;

        /// <summary>Annotation type groups shown as checkboxes.</summary>
        public ObservableCollection<AnnotationFamily> AnnotationFamilies { get; } = new();
        /// <summary>Overlapping pairs shown in the results grid.</summary>
        public ObservableCollection<OverlapResult> OverlapResults { get; } = new();

        /// <summary>
        /// v4.1.0: also compare different types (e.g. a tag over a text note).
        /// Off by default so results match v4.0.
        /// </summary>
        [ObservableProperty]
        private bool checkAcrossTypes;

        /// <summary>
        /// v4.1.0: ignore overlaps smaller than this on paper (mm). 0 (default) reports every overlap.
        /// </summary>
        [ObservableProperty]
        private double minOverlapPaperMm;

        /// <summary>Scale of the scanned view (e.g. 100 for 1:100).</summary>
        [ObservableProperty]
        private int viewScale = 1;

        /// <summary>Name and scale of the scanned view, for the window.</summary>
        [ObservableProperty]
        private string viewInfoText = string.Empty;

        [ObservableProperty]
        private int totalScanned;

        [ObservableProperty]
        private int totalOverlaps;

        [ObservableProperty]
        private string breakdownByTypeText = string.Empty;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        private bool hasError;

        // ToolWindowShell status strip (CLAUDE.md v1.1)
        [ObservableProperty]
        private double progress;

        [ObservableProperty]
        private string summaryText = string.Empty;

        // Raw collected data, kept around so "Check Overlaps" doesn't re-scan the document
        private List<AnnotationData> _allAnnotations = new();

        /// <summary>
        /// Creates the view model and scans the view. Must be called in a Revit API context.
        /// </summary>
        public AnnotationOverlapViewModel(Document doc, View activeView,
            ExternalEvent zoomEvent, ZoomToElementEventHandler zoomHandler,
            ExternalEvent refreshEvent, RefreshAnnotationsEventHandler refreshHandler)
        {
            _doc = doc;
            _activeView = activeView;
            _zoomEvent = zoomEvent;
            _zoomHandler = zoomHandler;
            _refreshEvent = refreshEvent;
            _refreshHandler = refreshHandler;

            _zoomHandler.TargetViewId = activeView.Id;
            _zoomHandler.ReportMessage = ShowMessage;
            _refreshHandler.Work = _ => RefreshInApiContext();

            LoadAnnotationFamilies(doc);
        }

        /// <summary>
        /// Step 2 + 3: scan the view and populate the type-selector list.
        /// Types the user had unticked stay unticked after a refresh. Must run in a Revit API context.
        /// </summary>
        public void LoadAnnotationFamilies(Document doc)
        {
            var unticked = AnnotationFamilies.Where(f => !f.IsSelected).Select(f => f.TypeName).ToHashSet();

            ViewScale = Math.Max(1, _activeView.Scale);
            ViewInfoText = $"View: {_activeView.Name}  ·  Scale 1:{ViewScale}";

            _allAnnotations = OverlapDetectionHelper.GetAnnotationsFromView(_activeView, doc);
            var grouped = OverlapDetectionHelper.GroupByType(_allAnnotations);

            AnnotationFamilies.Clear();
            foreach (var kvp in grouped.OrderBy(g => g.Key))
            {
                AnnotationFamilies.Add(new AnnotationFamily
                {
                    TypeName = kvp.Key,
                    Count = kvp.Value.Count,
                    IsSelected = !unticked.Contains(kvp.Key)
                });
            }
        }

        /// <summary>
        /// v4.1.0: re-scan the view (after annotations were moved, added or deleted).
        /// </summary>
        [RelayCommand]
        private void Refresh()
        {
            ShowMessage(null);
            IsLoading = true;
            _refreshEvent.Raise();
        }

        private void RefreshInApiContext()
        {
            try
            {
                if (_doc == null || !_doc.IsValidObject || _activeView == null || !_activeView.IsValidObject)
                {
                    ShowMessage("The scanned view or document is no longer available. Close this window and run the tool again.");
                    return;
                }

                LoadAnnotationFamilies(_doc);

                if (_hasRun)
                    CheckOverlaps();
                else
                    TotalScanned = _allAnnotations.Count;

                SummaryText = $"Refreshed: {_allAnnotations.Count} annotations in view.";
            }
            catch (Exception ex)
            {
                ShowMessage($"Refresh failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ShowMessage(string message)
        {
            HasError = !string.IsNullOrEmpty(message);
            ErrorMessage = message ?? string.Empty;
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var family in AnnotationFamilies)
                family.IsSelected = true;
        }

        [RelayCommand]
        private void ClearAll()
        {
            foreach (var family in AnnotationFamilies)
                family.IsSelected = false;
        }

        [RelayCommand]
        private void CheckOverlaps()
        {
            HasError = false;
            ErrorMessage = string.Empty;

            var selectedTypes = AnnotationFamilies
                .Where(f => f.IsSelected)
                .Select(f => f.TypeName)
                .ToHashSet();

            if (selectedTypes.Count == 0)
            {
                HasError = true;
                ErrorMessage = "Please select at least one type.";
                return;
            }

            if (double.IsNaN(MinOverlapPaperMm) || MinOverlapPaperMm < 0)
            {
                HasError = true;
                ErrorMessage = "Minimum overlap must be 0 or more.";
                return;
            }

            IsLoading = true;
            try
            {
                RunOverlapDetection(selectedTypes);
                UpdateSummaryCard();
                _hasRun = true;
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Overlap detection failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Step 5: run detection against only the selected type groups.
        /// </summary>
        private void RunOverlapDetection(HashSet<string> selectedTypes)
        {
            var filtered = _allAnnotations
                .Where(a => selectedTypes.Contains(a.TypeName))
                .ToList();

            var results = OverlapDetectionHelper.DetectOverlaps(filtered, CheckAcrossTypes, ViewScale, MinOverlapPaperMm);

            OverlapResults.Clear();
            foreach (var r in results)
                OverlapResults.Add(r);

            TotalScanned = filtered.Count;
        }

        /// <summary>
        /// Step 7: summary card - total scanned, total overlaps, breakdown by type.
        /// </summary>
        private void UpdateSummaryCard()
        {
            TotalOverlaps = OverlapResults.Count;

            var breakdown = OverlapResults
                .GroupBy(r => r.AnnotationType)
                .Select(g => $"{g.Count()} {g.Key}");

            BreakdownByTypeText = TotalOverlaps == 0
                ? "No overlaps found"
                : string.Join(", ", breakdown);
        }

        /// <summary>Zoom to a single element (kept for compatibility).</summary>
        [RelayCommand]
        private void ZoomToElement(long elementIdValue)
        {
            _zoomHandler.ElementIdsToZoom = new List<ElementId> { new ElementId(elementIdValue) };
            _zoomEvent.Raise();
        }

        /// <summary>v4.1.0: zoom to and select both elements of an overlapping pair.</summary>
        [RelayCommand]
        private void ZoomToPair(OverlapResult pair)
        {
            if (pair == null)
                return;

            _zoomHandler.ElementIdsToZoom = new List<ElementId>
            {
                new ElementId(pair.ElementId1),
                new ElementId(pair.ElementId2)
            };
            _zoomEvent.Raise();
        }
    }
}
