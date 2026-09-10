// File: AutoSlopeByDrainTabView.xaml.cs
// Location: UI/Views/Tabs/
// Base: extracted from AutoSlopeByDrain_V007's AutoSlopeByDrainWindow, converted
//   from a standalone Window into an embeddable UserControl for hosting as a tab
//   inside CombinedRoofTools_V001.
//
// CHANGES vs. the original AutoSlopeByDrainWindow.xaml.cs:
//   Window -> UserControl; the class no longer owns its own DataContext —
//     the combined window supplies it externally when this tab is selected.
//   REMOVED the viewModel constructor parameter and the _viewModel field —
//     use the parameterless constructor instead.
//   REMOVED OnClosing override — UserControl has no such lifecycle hook;
//     the combined window's own top-level Closing handler is responsible for
//     calling SaveSettingsOnClose() on this tab's ViewModel separately.
//   KEPT CopySelectedLogs_Click and DrainCheckBox_PreviewMouseLeftButtonDown
//     unchanged — both still work fine on a UserControl.

using Revit26_Plugin.AutoSlopeByDrain.V007.UI.ViewModels;
using Revit26_Plugin.Shared.Models; // LogEntry
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Revit26_Plugin.CombinedRoofTools.V001.UI.Views.Tabs
{
    public partial class AutoSlopeByDrainTabView : UserControl
    {
        public AutoSlopeByDrainTabView()
        {
            InitializeComponent();
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
