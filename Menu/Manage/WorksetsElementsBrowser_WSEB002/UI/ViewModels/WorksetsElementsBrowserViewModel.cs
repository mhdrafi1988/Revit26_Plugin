using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.WorksetsElementsBrowser.WSEB002.Core.Models;
using Revit26_Plugin.WorksetsElementsBrowser.WSEB002.Infrastructure.ExternalEvents;
using Revit26_Plugin.WorksetsElementsBrowser.WSEB002.UI.Views;

namespace Revit26_Plugin.WorksetsElementsBrowser.WSEB002.UI.ViewModels
{
    public partial class WorksetsElementsBrowserViewModel : ObservableObject, IDisposable
    {
        private readonly UIDocument _uiDoc;
        private readonly ExternalEvent _event;
        private readonly WorksetsElementsBrowserHandler _handler;
        private readonly Dispatcher _dispatcher;
        private readonly IntPtr _mainWindowHandle;

        /// <summary>Set right before Raise() to tell OnHandlerRequestCompleted what follow-up to run once results land.</summary>
        private Action _pendingCompletion;

        public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = new();

        [ObservableProperty] private string _documentTitle = string.Empty;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _summaryText = string.Empty;
        [ObservableProperty] private string _checkedSummaryText = "0 elements checked";

        public WorksetsElementsBrowserViewModel(UIDocument uiDoc, IntPtr mainWindowHandle)
        {
            _uiDoc = uiDoc;
            _mainWindowHandle = mainWindowHandle;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _handler = new WorksetsElementsBrowserHandler(uiDoc);
            _handler.RequestCompleted += OnHandlerRequestCompleted;
            _event = ExternalEvent.Create(_handler);

            DocumentTitle = uiDoc.Document.Title;

            RunLoadTree();
        }

        partial void OnSearchTextChanged(string value)
        {
            foreach (var root in RootNodes)
                root.ApplyFilter(value ?? string.Empty);
        }

        // ─────────────────────────────────────────────────────────────
        // Load
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Single choke point for every Raise() in this ViewModel. Guards
        /// against a second request starting while one is still in flight
        /// (e.g. a double-click on an action button) — without this, the
        /// second call would overwrite _handler's input properties and
        /// _pendingCompletion out from under the first request.
        /// </summary>
        private void RaiseRequest(WorksetsElementsBrowserRequest request, Action onCompleted)
        {
            if (IsBusy)
            {
                Infrastructure.ExternalEvents.WsebDebugLog.Write($"RaiseRequest({request}) SKIPPED: already IsBusy");
                return;
            }
            IsBusy = true;
            _handler.Request = request;
            _pendingCompletion = onCompleted;
            var raiseResult = _event.Raise();
            Infrastructure.ExternalEvents.WsebDebugLog.Write($"RaiseRequest({request}) -> Raise() returned {raiseResult}");
        }

        private void RunLoadTree() => RaiseRequest(WorksetsElementsBrowserRequest.LoadTree, HandleLoadTreeCompleted);

        private void HandleLoadTreeCompleted()
        {
            RootNodes.Clear();

            if (!_handler.LastRunSucceeded)
            {
                MessageBox.Show($"Could not load worksets and elements: {_handler.ErrorMessage}",
                    "Worksets & Elements Browser", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int worksetCount = 0, categoryCount = 0, typeCount = 0, elementCount = 0;

            foreach (var worksetData in _handler.LoadedTree)
            {
                worksetCount++;
                var worksetNode = new TreeNodeViewModel(
                    ElementTreeNodeKind.Workset,
                    worksetData.Name,
                    editableBadge: worksetData.IsEditable ? "editable" : "not editable",
                    owner: worksetData.Owner)
                {
                    IsExpanded = true
                };
                worksetNode.CheckedChanged += RefreshCheckedSummary;

                foreach (var categoryData in worksetData.Categories)
                {
                    categoryCount++;
                    var categoryNode = new TreeNodeViewModel(ElementTreeNodeKind.Category, categoryData.Name);
                    worksetNode.AddChild(categoryNode);

                    foreach (var typeData in categoryData.Types)
                    {
                        typeCount++;
                        elementCount += typeData.ElementIds.Count;
                        var typeNode = new TreeNodeViewModel(
                            ElementTreeNodeKind.Type,
                            typeData.Name,
                            elementCount: typeData.ElementIds.Count,
                            elementIds: typeData.ElementIds);
                        categoryNode.AddChild(typeNode);
                    }
                }

                RootNodes.Add(worksetNode);
            }

            SummaryText = $"{worksetCount} workset{(worksetCount == 1 ? "" : "s")} · " +
                           $"{categoryCount} categor{(categoryCount == 1 ? "y" : "ies")} · " +
                           $"{typeCount} type{(typeCount == 1 ? "" : "s")} · " +
                           $"{elementCount:N0} elements";

            RefreshCheckedSummary();
        }

        private void RefreshCheckedSummary()
        {
            var ids = CollectCheckedElementIds().ToList();
            int typeCount = RootNodes.SelectMany(FlattenTypes).Count(t => t.IsChecked == true);
            CheckedSummaryText = $"{typeCount} type{(typeCount == 1 ? "" : "s")} checked · {ids.Count:N0} elements";
        }

        private static IEnumerable<TreeNodeViewModel> FlattenTypes(TreeNodeViewModel node)
        {
            if (node.Kind == ElementTreeNodeKind.Type)
            {
                yield return node;
                yield break;
            }
            foreach (var child in node.Children)
                foreach (var t in FlattenTypes(child))
                    yield return t;
        }

        private List<ElementId> CollectCheckedElementIds()
            => RootNodes.SelectMany(r => r.CollectCheckedElementIds()).Distinct().ToList();

        // ─────────────────────────────────────────────────────────────
        // Toolbar commands
        // ─────────────────────────────────────────────────────────────
        [RelayCommand]
        private void ExpandAll() => SetExpandedRecursive(RootNodes, true);

        [RelayCommand]
        private void CollapseAll() => SetExpandedRecursive(RootNodes, false);

        [RelayCommand]
        private void CheckAll()
        {
            foreach (var root in RootNodes) root.IsChecked = true;
        }

        [RelayCommand]
        private void ClearChecks()
        {
            foreach (var root in RootNodes) root.IsChecked = false;
        }

        private static void SetExpandedRecursive(IEnumerable<TreeNodeViewModel> nodes, bool expanded)
        {
            foreach (var node in nodes)
            {
                if (node.Kind != ElementTreeNodeKind.Type)
                    node.IsExpanded = expanded;
                SetExpandedRecursive(node.Children, expanded);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Checked-rows actions
        // ─────────────────────────────────────────────────────────────
        [RelayCommand]
        private void IsolateChecked() => RunCheckedAction(ElementActionMode.Isolate);

        [RelayCommand]
        private void SelectChecked() => RunCheckedAction(ElementActionMode.Select);

        [RelayCommand]
        private void IsolateAndSelectChecked() => RunCheckedAction(ElementActionMode.IsolateAndSelect);

        /// <summary>The dedicated toolbar action requested alongside the three checked-rows buttons — always Isolate + Select, the "just show me everything checked" one-click case.</summary>
        [RelayCommand]
        private void ShowCheckedIn3D() => RunCheckedAction(ElementActionMode.IsolateAndSelect);

        private void RunCheckedAction(ElementActionMode mode)
        {
            var ids = CollectCheckedElementIds();
            if (ids.Count == 0)
            {
                MessageBox.Show("No elements are checked. Check one or more types first.",
                    "Worksets & Elements Browser", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ShowInThreeD($"Show all {ids.Count:N0} checked element(s) in a 3D view?", ids, mode);
        }

        /// <summary>Invoked from the View's code-behind when the user clicks a Type row's name, or its dedicated "Show in 3D" button.</summary>
        public void OnTypeRowClicked(TreeNodeViewModel typeNode)
        {
            if (typeNode.Kind != ElementTreeNodeKind.Type) return;

            string prompt = $"Show all {typeNode.ElementIds.Count:N0} elements of \"{typeNode.Name}\" in a 3D view?";
            var result = MessageBox.Show(prompt, "Worksets & Elements Browser", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            ShowInThreeD(prompt, typeNode.ElementIds.ToList(), ElementActionMode.IsolateAndSelect);
        }

        /// <summary>
        /// Loads the model's 3D views, lets the user pick one, then applies
        /// Isolate/Select/Both to it. Two Revit-API round trips chained
        /// through RaiseRequest — never call ExternalEvent.Raise() directly
        /// from inside a completion callback (see OnHandlerRequestCompleted).
        /// </summary>
        private void ShowInThreeD(string prompt, List<ElementId> ids, ElementActionMode mode)
        {
            RaiseRequest(WorksetsElementsBrowserRequest.LoadViews, () =>
            {
                if (!_handler.LastRunSucceeded)
                {
                    MessageBox.Show($"Could not load 3D views: {_handler.ErrorMessage}",
                        "Worksets & Elements Browser", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_handler.LoadedViews.Count == 0)
                {
                    MessageBox.Show("This model has no 3D views to show elements in.",
                        "Worksets & Elements Browser", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var picker = new View3DPickerWindow();
                new System.Windows.Interop.WindowInteropHelper(picker).Owner = _mainWindowHandle;
                picker.Initialize(prompt, _handler.LoadedViews);
                bool? dialogResult = picker.ShowDialog();
                Infrastructure.ExternalEvents.WsebDebugLog.Write($"Picker closed: dialogResult={dialogResult}, SelectedView={picker.SelectedView?.Name}");
                if (dialogResult != true || picker.SelectedView == null) return;

                _handler.ActionElementIds = ids;
                _handler.TargetViewId = picker.SelectedView.ViewId;
                _handler.ActionMode = mode;
                Infrastructure.ExternalEvents.WsebDebugLog.Write($"About to RaiseRequest(ApplyAction), IsBusy={IsBusy}, ids.Count={ids.Count}, mode={mode}");
                RaiseRequest(WorksetsElementsBrowserRequest.ApplyAction, HandleApplyActionCompleted);
            });
        }

        private void HandleApplyActionCompleted()
        {
            Infrastructure.ExternalEvents.WsebDebugLog.Write($"HandleApplyActionCompleted: LastRunSucceeded={_handler.LastRunSucceeded}, ErrorMessage={_handler.ErrorMessage}");
            if (!_handler.LastRunSucceeded)
            {
                MessageBox.Show($"Could not show elements in the selected view: {_handler.ErrorMessage}",
                    "Worksets & Elements Browser", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // Window stays open per this project's window-lifecycle convention.
        }

        // ─────────────────────────────────────────────────────────────
        // Window close
        // ─────────────────────────────────────────────────────────────
        [RelayCommand]
        private void Close() => CloseRequested?.Invoke();

        public event Action CloseRequested;

        /// <summary>
        /// Fires from inside the handler's own Execute(UIApplication), on
        /// Revit's UI thread but still nested in that call stack — Revit
        /// denies/ignores ExternalEvent.Raise() called reentrantly like that,
        /// and a modal ShowDialog() opened from the same nested frame is
        /// unreliable too. BeginInvoke (not Invoke) defers the continuation
        /// — including any chained Raise() for a follow-up request and any
        /// ShowDialog() — to run only after Execute() has fully returned and
        /// control is back with Revit's own message loop. Without this, the
        /// LoadViews -> pick a view -> ApplyAction chain silently did
        /// nothing: views never populated and isolate/select never ran.
        /// </summary>
        private void OnHandlerRequestCompleted()
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                IsBusy = false;
                var completion = _pendingCompletion;
                _pendingCompletion = null;
                completion?.Invoke();
            }));
        }

        public void Dispose()
        {
            _handler.RequestCompleted -= OnHandlerRequestCompleted;
            _event?.Dispose();
        }
    }
}
