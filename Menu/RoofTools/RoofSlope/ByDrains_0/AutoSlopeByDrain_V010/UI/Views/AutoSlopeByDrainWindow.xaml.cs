// File: AutoSlopeByDrainWindow.xaml.cs
// Location: UI/Views/
// Base: ported from AutoSlopeByDrain V004.
//
// CHANGES (V005):
//   REMOVED CopyAllLogs_Click — "Copy all" is now a [RelayCommand] on the
//     ViewModel (CopyAllLogsCommand) since it needs no code-behind access;
//     only "Copy selected" still needs code-behind (it reads
//     LogListBox.SelectedItems, which isn't cleanly bindable).
//   ADDED   OnClosing override — calls ViewModel.SaveSettingsOnClose() so
//     settings are captured even if the user closes the window without
//     ever clicking Run this session.
//   ADDED   DrainCheckBox_PreviewMouseLeftButtonDown — blocks the DataGrid
//     row-select cascade on checkbox click, per Rafi's DataGrid spec.
//
// NEW (V010), per Rafi's confirmed group-expand decision:
//   ADDED DrainGroupExpander_Loaded / _ExpandedOrCollapsed — drives each
//     group header's IsExpanded from that roof tab's GroupExpandedOverride
//     dictionary (RoofTabViewModel), falling back to "this is the first
//     group in the current filtered view" when the user hasn't manually
//     toggled it. Needs code-behind because the Expander's own DataContext
//     is the CollectionViewGroup, not the RoofTabViewModel — the owning
//     RoofTabViewModel is found by walking up to the enclosing DataGrid.

using Revit26_Plugin.MultiRoofSlopeByDrain.V010.UI.ViewModels;
using Revit26_Plugin.Shared.Models; // LogEntry
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Revit26_Plugin.MultiRoofSlopeByDrain.V010.UI.Views
{
    public partial class AutoSlopeByDrainWindow : Window
    {
        private readonly AutoSlopeDrainViewModel _viewModel;

        public AutoSlopeByDrainWindow(AutoSlopeDrainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            this.Focus();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _viewModel?.SaveSettingsOnClose();
            base.OnClosing(e);
        }

        // ── Copy Selected (rows highlighted in the log ListBox) ─────────────────
        // Kept as code-behind (not a [RelayCommand]) because it needs direct
        // access to LogListBox.SelectedItems, which ListBox doesn't expose as a
        // bindable dependency property.
        private void CopySelectedLogs_Click(object sender, RoutedEventArgs e)
        {
            var selected = LogListBox.SelectedItems.Cast<LogEntry>().ToList();
            if (selected.Count == 0) return;

            string text = string.Join(Environment.NewLine, selected.Select(entry => entry.ToString()));
            TrySetClipboardText(text);
        }

        // ── DataGrid checkbox: block row-select cascade ─────────────────────────
        // Per Rafi's DataGrid spec: clicking the checkbox should toggle selection
        // only, not also trigger the DataGrid's native row-selection behavior.
        // PreviewMouseLeftButtonDown is a TUNNELING event — it fires BEFORE the
        // CheckBox's own click handling, and setting e.Handled = true here
        // suppresses that later handling entirely. So IsChecked must be toggled
        // manually right here; it is NOT redundant with the CheckBox's own logic.
        private void DrainCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                cb.IsChecked = !(cb.IsChecked ?? false);
                e.Handled = true;
            }
        }

        private static void TrySetClipboardText(string text)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
            }
            catch
            {
                // Clipboard can be transiently locked by another process — fail silently,
                // consistent with this suite's "never block the UI on a non-critical error" rule.
            }
        }

        // ── Drain group Expander default/sticky state — NEW (V010) ─────────────

        /// <summary>
        /// Fires whenever a group's Expander container is (re)created — including
        /// after FilteredDrainsView.Refresh() on a size-filter change, and fresh
        /// for whichever roof tab's DataGrid just became the active tab (WPF's
        /// TabControl only materializes the selected tab's content). Sets
        /// IsExpanded from the owning RoofTabViewModel's GroupExpandedOverride if
        /// the user has manually toggled this group name before; otherwise
        /// defaults to "this is the first group in the current view" — which is
        /// Circle when present (ShapeGroupOrder sorts it first) and whichever
        /// group sorts first otherwise, satisfying both "Circle opens by default"
        /// and "open the first group when there's no Circle" with one rule.
        /// </summary>
        private void DrainGroupExpander_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is Expander expander)) return;
            if (!(expander.DataContext is CollectionViewGroup group)) return;

            var tabVm = FindOwningRoofTabViewModel(expander);
            if (tabVm == null) return;

            string groupName = group.Name?.ToString() ?? "";
            bool isFirstGroupInView = tabVm.FilteredDrainsView.Groups?.Count > 0
                && (tabVm.FilteredDrainsView.Groups[0] as CollectionViewGroup)?.Name?.ToString() == groupName;

            expander.IsExpanded = tabVm.GroupExpandedOverride.TryGetValue(groupName, out bool overridden)
                ? overridden
                : isFirstGroupInView;
        }

        /// <summary>
        /// Fires on every user-driven expand/collapse (also fires once from the
        /// Loaded handler's own IsExpanded set above, which just re-records the
        /// same value — harmless). Records the override so it sticks through
        /// Select All/None/Invert and the per-group All/None buttons, per Rafi's
        /// confirmed decision, and is cleared only by RoofTabViewModel on a
        /// size-filter change.
        /// </summary>
        private void DrainGroupExpander_ExpandedOrCollapsed(object sender, RoutedEventArgs e)
        {
            if (!(sender is Expander expander)) return;
            if (!(expander.DataContext is CollectionViewGroup group)) return;

            var tabVm = FindOwningRoofTabViewModel(expander);
            if (tabVm == null) return;

            tabVm.GroupExpandedOverride[group.Name?.ToString() ?? ""] = expander.IsExpanded;
        }

        /// <summary>Walks up the visual tree from a group Expander to the enclosing DataGrid, whose DataContext is that roof's RoofTabViewModel.</summary>
        private static RoofTabViewModel FindOwningRoofTabViewModel(DependencyObject start)
        {
            for (DependencyObject current = start; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is DataGrid dataGrid && dataGrid.DataContext is RoofTabViewModel tabVm)
                    return tabVm;
            }
            return null;
        }
    }
}

