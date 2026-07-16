// =======================================================
// File: UI_Autoslope.xaml.cs
// Fixes:
//   #1  Corrected namespace to match XAML (041)
//   #2  Removed unused Action<string> log parameter from
//       AutoSlopeViewModel constructor call.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.V020.UI.ViewModels;   // ensure ViewModel namespace matches
using Revit26_Plugin.Shared.Models;                          // LogEntry
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByPoint.V020.UI.Views      // ✅ changed from _04 to _041
{
    public partial class AutoSlopeWindow : Window
    {
        public AutoSlopeWindow(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drains)
        {
            InitializeComponent();

            var viewModel = new AutoSlopeViewModel(uidoc, app, roofId, drains);
            DataContext = viewModel;
            this.Focus();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            // placeholder for any toggle logic
        }

        // ── Copy All ──────────────────────────────────────────────────────────
        private void CopyAllLogs_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AutoSlopeViewModel vm || vm.LogEntries.Count == 0) return;

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