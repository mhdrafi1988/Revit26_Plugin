// File: AutoSlopeDrainWindow.xaml.cs
// Location: UI/Views/
// Mirrors AutoSlopeByPoint's UI_Autoslope.xaml.cs Copy All / Copy Selected pattern.

using Revit26_Plugin.AutoSlopeByDrain.V004.UI.ViewModels;
using Revit26_Plugin.Shared.Models; // LogEntry
using System;
using System.Linq;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByDrain.V004.UI.Views
{
    public partial class AutoSlopeDrainWindow : Window
    {
        public AutoSlopeDrainWindow(AutoSlopeDrainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            this.Focus();
        }

        // ── Copy All ──────────────────────────────────────────────────────────
        private void CopyAllLogs_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AutoSlopeDrainViewModel vm || vm.LogEntries.Count == 0) return;

            string text = string.Join(Environment.NewLine, vm.LogEntries.Select(entry => entry.ToString()));
            TrySetClipboardText(text);
        }

        // ── Copy Selected (rows highlighted in the log ListBox) ─────────────────
        private void CopySelectedLogs_Click(object sender, RoutedEventArgs e)
        {
            var selected = LogListBox.SelectedItems.Cast<LogEntry>().ToList();
            if (selected.Count == 0) return;

            string text = string.Join(Environment.NewLine, selected.Select(entry => entry.ToString()));
            TrySetClipboardText(text);
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
