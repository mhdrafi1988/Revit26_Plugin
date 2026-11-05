using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Helpers;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Models;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Views;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.ViewModels
{
    /// <summary>
    /// View model for the Annotation Overlap Detection window.
    /// Since v4.2.0 a scan can cover several views (batch check by sheets and/or views),
    /// and leaders can be left out of the check. Since v4.3.0 results can be exported and
    /// the clashing elements highlighted red in Revit.
    /// </summary>
    public partial class AnnotationOverlapViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly string _docTitle;
        private readonly List<ElementId> _startScope;
        private readonly ExternalEvent _zoomEvent;
        private readonly ZoomToElementEventHandler _zoomHandler;
        private readonly ExternalEvent _refreshEvent;
        private readonly RefreshAnnotationsEventHandler _refreshHandler;

        private List<ElementId> _scopeViewIds;
        private bool _hasRun;
        private bool _scannedWithLeaders;

        /// <summary>Annotation type groups shown as checkboxes.</summary>
        public ObservableCollection<AnnotationFamily> AnnotationFamilies { get; } = new();

        /// <summary>Overlapping pairs shown in the results grid.</summary>
        public ObservableCollection<OverlapResult> OverlapResults { get; } = new();

        /// <summary>
        /// v4.1.0: also compare different types (e.g. a tag over a text note). Off by default.
        /// </summary>
        [ObservableProperty]
        private bool checkAcrossTypes;

        /// <summary>
        /// v4.1.0: ignore overlaps smaller than this on paper (mm). 0 (default) reports every overlap.
        /// </summary>
        [ObservableProperty]
        private double minOverlapPaperMm;

        /// <summary>
        /// v4.2.0: include tag / text-note leaders in the check. Off by default, so only the
        /// tag head / text box is checked.
        /// </summary>
        [ObservableProperty]
        private bool includeLeaders;

        /// <summary>What is being checked: view name and scale, or the number of views in a batch.</summary>
        [ObservableProperty]
        private string viewInfoText = string.Empty;

        /// <summary>True when the scope is something other than the starting view(s).</summary>
        [ObservableProperty]
        private bool isBatchScope;

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

        // Raw collected data, kept so "Check Overlaps" doesn't re-scan when nothing changed
        private List<AnnotationData> _allAnnotations = new();

        /// <summary>
        /// Creates the view model and scans the starting views. Must be called in a Revit API context.
        /// </summary>
        /// <param name="startScope">Views to check first: the active view, or the views on the active sheet.</param>
        public AnnotationOverlapViewModel(Document doc, IEnumerable<View> startScope,
            ExternalEvent zoomEvent, ZoomToElementEventHandler zoomHandler,
            ExternalEvent refreshEvent, RefreshAnnotationsEventHandler refreshHandler)
        {
            _doc = doc;
            _docTitle = doc.Title;
            _startScope = startScope.Select(v => v.Id).ToList();
            _scopeViewIds = new List<ElementId>(_startScope);
            _zoomEvent = zoomEvent;
            _zoomHandler = zoomHandler;
            _refreshEvent = refreshEvent;
            _refreshHandler = refreshHandler;

            _zoomHandler.ReportMessage = ShowMessage;

            ScanScope();
        }

        /// <summary>
        /// Step 2 + 3: scan the views in scope and populate the type-selector list.
        /// Types the user had unticked stay unticked. Must run in a Revit API context.
        /// </summary>
        private void ScanScope()
        {
            var unticked = AnnotationFamilies.Where(f => !f.IsSelected).Select(f => f.TypeName).ToHashSet();

            var views = _scopeViewIds
                .Select(id => _doc.GetElement(id) as View)
                .Where(v => v != null && v.IsValidObject)
                .ToList();

            var scan = AnnotationScanner.Scan(_doc, views, IncludeLeaders);
            _allAnnotations = scan.Annotations;
            _scannedWithLeaders = IncludeLeaders;

            ViewInfoText = views.Count == 1
                ? $"View: {views[0].Name}  ·  Scale 1:{Math.Max(1, views[0].Scale)}"
                : $"{views.Count} views";
            IsBatchScope = !_scopeViewIds.SequenceEqual(_startScope);

            AnnotationFamilies.Clear();
            foreach (var g in _allAnnotations.GroupBy(a => a.TypeName).OrderBy(g => g.Key))
            {
                AnnotationFamilies.Add(new AnnotationFamily
                {
                    TypeName = g.Key,
                    Count = g.Count(),
                    IsSelected = !unticked.Contains(g.Key)
                });
            }

            TotalScanned = _allAnnotations.Count;
            SummaryText = $"Scanned {_allAnnotations.Count} annotations in {views.Count} view(s).";

            if (views.Count < _scopeViewIds.Count)
                scan.Warnings.Insert(0, $"{_scopeViewIds.Count - views.Count} view(s) no longer exist and were skipped.");
            ShowMessage(scan.Warnings.Count == 0 ? null : string.Join(Environment.NewLine, scan.Warnings.Take(5))
                + (scan.Warnings.Count > 5 ? $"{Environment.NewLine}… and {scan.Warnings.Count - 5} more." : string.Empty));
        }

        /// <summary>Runs work in the Revit API context through the external event.</summary>
        private void RunInApiContext(Action<UIApplication> work)
        {
            IsLoading = true;
            _refreshHandler.Work = app =>
            {
                try
                {
                    if (_doc == null || !_doc.IsValidObject)
                    {
                        ShowMessage("The document is no longer open. Close this window and run the tool again.");
                        return;
                    }
                    work(app);
                }
                catch (Exception ex)
                {
                    ShowMessage($"Failed: {ex.Message}");
                }
                finally
                {
                    IsLoading = false;
                }
            };
            _refreshEvent.Raise();
        }

        /// <summary>v4.1.0: re-scan the current scope (after annotations were moved, added or deleted).</summary>
        [RelayCommand]
        private void Refresh()
        {
            ShowMessage(null);
            RunInApiContext(_ =>
            {
                ScanScope();
                if (_hasRun)
                    RunCheck();
            });
        }

        /// <summary>v4.2.0: choose sheets and/or views to check together.</summary>
        [RelayCommand]
        private void BatchCheck()
        {
            ShowMessage(null);
            RunInApiContext(app =>
            {
                var picker = BuildPicker();
                if (picker.Sheets.Count == 0 && picker.Views.Count == 0)
                {
                    ShowMessage("No sheets or supported views found in this model.");
                    return;
                }

                var window = new BatchScopePicker(picker);
                try { new WindowInteropHelper(window).Owner = app.MainWindowHandle; } catch (Exception) { /* owner is cosmetic */ }
                window.ShowDialog();
                if (!picker.Accepted)
                    return;

                _scopeViewIds = picker.SelectedViewIds().Select(id => new ElementId(id)).ToList();
                OverlapResults.Clear();
                ScanScope();
                RunCheck();
            });
        }

        /// <summary>v4.2.0: go back to the view(s) the tool was started in.</summary>
        [RelayCommand]
        private void BackToStartView()
        {
            ShowMessage(null);
            RunInApiContext(_ =>
            {
                _scopeViewIds = new List<ElementId>(_startScope);
                OverlapResults.Clear();
                _hasRun = false;
                ScanScope();
            });
        }

        private BatchScopePickerViewModel BuildPicker()
        {
            var views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(OverlapDetectionHelper.IsSupportedView)
                .OrderBy(v => v.ViewType.ToString())
                .ThenBy(v => v.Name)
                .ToList();
            var supportedIds = views.Select(v => v.Id.Value).ToHashSet();

            var viewItems = views.Select(v => new ScopeItem
            {
                Id = v.Id.Value,
                Name = v.Name,
                Detail = $"{v.ViewType}  ·  1:{Math.Max(1, v.Scale)}"
            });

            var sheetItems = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder && !s.IsTemplate)
                .Select(s => new
                {
                    Sheet = s,
                    Ids = s.GetAllPlacedViews().Select(id => id.Value).Where(supportedIds.Contains).ToArray()
                })
                .Where(x => x.Ids.Length > 0)
                .OrderBy(x => x.Sheet.SheetNumber)
                .Select(x => new ScopeItem
                {
                    Id = x.Sheet.Id.Value,
                    Name = $"{x.Sheet.SheetNumber} - {x.Sheet.Name}",
                    Detail = $"{x.Ids.Length} view(s)",
                    ViewIds = x.Ids
                });

            return new BatchScopePickerViewModel(sheetItems.ToList(), viewItems.ToList());
        }

        /// <summary>v4.3.0: save the results to Excel (.xlsx) or CSV.</summary>
        [RelayCommand]
        private void Export()
        {
            ShowMessage(null);
            if (OverlapResults.Count == 0)
            {
                ShowMessage("Nothing to export - run Check Overlaps first.");
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export overlap results",
                Filter = "Excel workbook (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv",
                FileName = MakeSafeFileName($"{_docTitle} - Annotation Overlaps") + ".xlsx",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                OverlapExportService.Export(dialog.FileName, OverlapResults.ToList(), new OverlapExportService.ExportInfo
                {
                    DocumentTitle = _docTitle,
                    ToolVersion = global::Revit26_Plugin.Shared.Services.ToolCatalog.AnnotationOverlapDetection.Version,
                    Scope = ViewInfoText,
                    CheckAcrossTypes = CheckAcrossTypes,
                    IncludeLeaders = _scannedWithLeaders,
                    MinOverlapPaperMm = MinOverlapPaperMm
                });
                SummaryText = $"Exported {OverlapResults.Count} overlaps to {System.IO.Path.GetFileName(dialog.FileName)}.";
            }
            catch (System.IO.IOException ex)
            {
                ShowMessage($"Could not save the file - is it open in another program? ({ex.Message})");
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowMessage($"No permission to save there: {ex.Message}");
            }
        }

        /// <summary>v4.3.0: colour both elements of every listed overlap red in their view.</summary>
        [RelayCommand]
        private void Highlight()
        {
            ShowMessage(null);
            if (OverlapResults.Count == 0)
            {
                ShowMessage("Nothing to highlight - run Check Overlaps first.");
                return;
            }

            var byView = new Dictionary<ElementId, HashSet<ElementId>>();
            foreach (var r in OverlapResults)
            {
                var viewId = new ElementId(r.ViewId);
                if (!byView.TryGetValue(viewId, out var set))
                    byView[viewId] = set = new HashSet<ElementId>();
                set.Add(new ElementId(r.ElementId1));
                set.Add(new ElementId(r.ElementId2));
            }

            RunInApiContext(_ =>
            {
                var outcome = HighlightService.Highlight(_doc, byView);
                SummaryText = $"Highlighted {outcome.Elements} element(s) red in {outcome.Views} view(s). Use Clear Highlight to remove.";
                if (outcome.Warnings.Count > 0)
                    ShowMessage(string.Join(Environment.NewLine, outcome.Warnings));
            });
        }

        /// <summary>v4.3.0: remove every highlight this tool made in the model and restore the original colours.</summary>
        [RelayCommand]
        private void ClearHighlight()
        {
            ShowMessage(null);
            RunInApiContext(_ =>
            {
                var outcome = HighlightService.ClearAll(_doc);
                SummaryText = outcome.Views == 0 && outcome.Warnings.Count == 0
                    ? "No highlights to clear."
                    : $"Cleared highlight on {outcome.Elements} element(s) in {outcome.Views} view(s).";
                if (outcome.Warnings.Count > 0)
                    ShowMessage(string.Join(Environment.NewLine, outcome.Warnings));
            });
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
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
            ShowMessage(null);

            if (!AnnotationFamilies.Any(f => f.IsSelected))
            {
                ShowMessage("Please select at least one type.");
                return;
            }

            if (double.IsNaN(MinOverlapPaperMm) || MinOverlapPaperMm < 0)
            {
                ShowMessage("Minimum overlap must be 0 or more.");
                return;
            }

            // The leaders option changes how boxes are measured, so it needs a re-scan.
            if (IncludeLeaders != _scannedWithLeaders)
            {
                RunInApiContext(_ =>
                {
                    ScanScope();
                    RunCheck();
                });
                return;
            }

            IsLoading = true;
            try
            {
                RunCheck();
            }
            catch (Exception ex)
            {
                ShowMessage($"Overlap detection failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Step 5 + 7: run detection against the selected type groups and update the summary card.
        /// </summary>
        private void RunCheck()
        {
            var selectedTypes = AnnotationFamilies
                .Where(f => f.IsSelected)
                .Select(f => f.TypeName)
                .ToHashSet();

            var filtered = _allAnnotations
                .Where(a => selectedTypes.Contains(a.TypeName))
                .ToList();

            var results = OverlapDetectionHelper.DetectOverlaps(filtered, CheckAcrossTypes, MinOverlapPaperMm);

            OverlapResults.Clear();
            foreach (var r in results)
                OverlapResults.Add(r);

            TotalScanned = filtered.Count;
            TotalOverlaps = OverlapResults.Count;

            var breakdown = OverlapResults
                .GroupBy(r => r.AnnotationType)
                .Select(g => $"{g.Count()} {g.Key}");

            int viewsWithOverlaps = OverlapResults.Select(r => r.ViewId).Distinct().Count();
            BreakdownByTypeText = TotalOverlaps == 0
                ? "No overlaps found"
                : string.Join(", ", breakdown) + (_scopeViewIds.Count > 1 ? $"  ·  in {viewsWithOverlaps} view(s)" : string.Empty);

            _hasRun = true;
        }

        /// <summary>Zoom to a single element in the first scanned view (kept for compatibility).</summary>
        [RelayCommand]
        private void ZoomToElement(long elementIdValue)
        {
            _zoomHandler.TargetViewId = _scopeViewIds.FirstOrDefault();
            _zoomHandler.ElementIdsToZoom = new List<ElementId> { new ElementId(elementIdValue) };
            _zoomEvent.Raise();
        }

        /// <summary>v4.1.0: zoom to and select both elements of a pair, opening its view if needed.</summary>
        [RelayCommand]
        private void ZoomToPair(OverlapResult pair)
        {
            if (pair == null)
                return;

            _zoomHandler.TargetViewId = new ElementId(pair.ViewId);
            _zoomHandler.ElementIdsToZoom = new List<ElementId>
            {
                new ElementId(pair.ElementId1),
                new ElementId(pair.ElementId2)
            };
            _zoomEvent.Raise();
        }
    }
}
