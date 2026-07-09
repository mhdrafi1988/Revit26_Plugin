// =======================================================
// File: UI_Autoslope.xaml.cs
// Changes vs V011:
//   ADDED  CopyAllLogs_Click / CopySelectedLogs_Click — clipboard
//          handlers for the new log panel buttons. Kept in
//          code-behind (not RelayCommand) since they need direct
//          access to ListBox.Items / ListBox.SelectedItems.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.V016.UI.ViewModels;   // ensure ViewModel namespace matches
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByPoint.V016.UI.Views
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

        private void CopyAllLogs_Click(object sender, RoutedEventArgs e)
        {
            CopyEntriesToClipboard(LogListBox.Items.Cast<LogEntry>());
        }

        private void CopySelectedLogs_Click(object sender, RoutedEventArgs e)
        {
            if (LogListBox.SelectedItems.Count == 0)
            {
                MessageBox.Show("No log rows selected.", "Copy Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            CopyEntriesToClipboard(LogListBox.SelectedItems.Cast<LogEntry>());
        }

        private static void CopyEntriesToClipboard(IEnumerable<LogEntry> entries)
        {
            var sb = new StringBuilder();
            foreach (var entry in entries)
                sb.AppendLine(entry.ToString());

            try
            {
                Clipboard.SetText(sb.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not copy to clipboard: " + ex.Message,
                    "Copy Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}