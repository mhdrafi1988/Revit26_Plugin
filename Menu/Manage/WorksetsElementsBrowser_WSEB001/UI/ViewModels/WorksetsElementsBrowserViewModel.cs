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
using Revit26_Plugin.WorksetsElementsBrowser.WSEB001.Core.Models;
using Revit26_Plugin.WorksetsElementsBrowser.WSEB001.Infrastructure.ExternalEvents;
using Revit26_Plugin.WorksetsElementsBrowser.WSEB001.UI.Views;

namespace Revit26_Plugin.WorksetsElementsBrowser.WSEB001.UI.ViewModels
{
    public partial class WorksetsElementsBrowserViewModel : ObservableObject, IDisposable
    {
        private readonly UIDocument _uiDoc;
        private readonly ExternalEvent _event;
        private readonly WorksetsElementsBrowserHandler _handler;
        private readonly Dispatcher _dispatcher;

        /// <summary>Set right before Raise() to tell OnHandlerRequestCompleted what follow-up to run once results land.</summary>
        private Action _pendingCompletion;

        public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = new();

        [ObservableProperty] private string _documentTitle = string.Empty;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _summaryText = string.Empty;
        [ObservableProperty] private string _checkedSummaryText = "0 elements checked";

        public WorksetsElementsBrowserViewModel(UIDocument uiDoc)
        {
            _uiDoc = uiDoc;
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
        private void RunLoadTree()
        {
            IsBusy = true;
            _handler.Request = WorksetsElementsBrowserRequest.LoadTree;
            _pendingCompletion = HandleLoadTreeCompleted;
            _event.Raise();
        }

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

        private void RunCheckedAction(ElementActionMode mode)
        {
            var ids = CollectCheckedElementIds();
            if (ids.Count == 0)
            {
                MessageBox.Show("No elements are checked. Check one or more types first.",
                    "Worksets & Elements Browser", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            PromptForViewAndApply($"Show all {ids.Count:N0} checked element(s) in a 3D view?", ids, mode);
        }

        /// <summary>Invoked from the View's code-behind when the user clicks a Type row directly.</summary>
        public void OnTypeRowClicked(TreeNodeViewModel typeNode)
        {
            if (typeNode.Kind != ElementTreeNodeKind.Type) return;

            var result = MessageBox.Show(
                $"Show all {typeNode.ElementIds.Count:N0} elements of \"{typeNode.Name}\" in a 3D view?",
                "Worksets & Elements Browser", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            PromptForViewAndApply(
                $"Show all {typeNode.ElementIds.Count:N0} elements of \"{typeNode.Name}\" in a 3D view?",
                typeNode.ElementIds.ToList(),
                ElementActionMode.IsolateAndSelect);
        }

        private void PromptForViewAndApply(string prompt, List<ElementId> ids, ElementActionMode mode)
        {
            IsBusy = true;
            _handler.Request = WorksetsElementsBrowserRequest.LoadViews;
            _pendingCompletion = () => HandleLoadViewsCompleted(prompt, ids, mode);
            _event.Raise();
        }

        private void HandleLoadViewsCompleted(string prompt, List<ElementId> ids, ElementActionMode mode)
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

            var picker = new View3DPickerWindow { Owner = Application.Current?.MainWindow };
            picker.Initialize(prompt, _handler.LoadedViews);
            bool? dialogResult = picker.ShowDialog();
            if (dialogResult != true || picker.SelectedView == null) return;

            IsBusy = true;
            _handler.ActionElementIds = ids;
            _handler.TargetViewId = picker.SelectedView.ViewId;
            _handler.ActionMode = mode;
            _handler.Request = WorksetsElementsBrowserRequest.ApplyAction;
            _pendingCompletion = HandleApplyActionCompleted;
            _event.Raise();
        }

        private void HandleApplyActionCompleted()
        {
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

        private void OnHandlerRequestCompleted()
        {
            _dispatcher.Invoke(() =>
            {
                IsBusy = false;
                var completion = _pendingCompletion;
                _pendingCompletion = null;
                completion?.Invoke();
            });
        }

        public void Dispose()
        {
            _handler.RequestCompleted -= OnHandlerRequestCompleted;
            _event?.Dispose();
        }
    }
}
