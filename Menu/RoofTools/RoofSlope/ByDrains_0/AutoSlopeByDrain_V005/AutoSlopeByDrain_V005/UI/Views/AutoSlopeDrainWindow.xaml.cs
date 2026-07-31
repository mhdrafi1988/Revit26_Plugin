// File: AutoSlopeDrainWindow.xaml.cs
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

using Revit26_Plugin.AutoSlopeByDrain.V005.UI.ViewModels;
using Revit26_Plugin.Shared.Models; // LogEntry
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Revit26_Plugin.AutoSlopeByDrain.V005.UI.Views
{
    public partial class AutoSlopeDrainWindow : Window
    {
        private readonly AutoSlopeDrainViewModel _viewModel;

        public AutoSlopeDrainWindow(AutoSlopeDrainViewModel viewModel)
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
                Clipboard.SetText(text);
            }
            catch
            {
                // Clipboard can be transiently locked by another process — fail silently,
                // consistent with this suite's "never block the UI on a non-critical error" rule.
            }
        }
    }
}
