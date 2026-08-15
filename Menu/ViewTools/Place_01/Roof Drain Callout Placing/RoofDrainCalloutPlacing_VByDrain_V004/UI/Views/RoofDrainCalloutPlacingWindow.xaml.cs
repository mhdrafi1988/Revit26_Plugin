using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Models;
using Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Services;
using Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.ViewModels;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Views
{
    /// <summary>
    /// Code-behind for RoofDrainCalloutPlacingWindow.
    /// - Handles checkbox interactions (prevent row-select cascade)
    /// - Group-local Select All/None button clicks (button Tag = group key)
    /// - Log copying and selection management
    /// - Window close event (persist settings, now per-group sizing)
    /// </summary>
    public partial class RoofDrainCalloutPlacingWindow : Window
    {
        public RoofDrainCalloutPlacingWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Prevent checkbox clicks from selecting the entire row.
        /// User must explicitly click the checkbox to toggle; row clicks do nothing.
        /// </summary>
        private void OpeningCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (sender is CheckBox checkBox)
            {
                checkBox.IsChecked = !checkBox.IsChecked;

                if (DataContext is RoofDrainCalloutPlacingViewModel vm)
                {
                    vm.UpdateMetrics();
                }
            }
        }

        /// <summary>
        /// Group-local "All" button — DataContext on the button is the owning
        /// OpeningGroupViewModel (button sits inside that group's DataTemplate).
        /// </summary>
        private void GroupSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe
                && fe.DataContext is OpeningGroupViewModel group
                && DataContext is RoofDrainCalloutPlacingViewModel vm)
            {
                vm.SelectAllInGroup(group);
            }
        }

        /// <summary>
        /// Group-local "None" button — same DataContext pattern as GroupSelectAll_Click.
        /// </summary>
        private void GroupSelectNone_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe
                && fe.DataContext is OpeningGroupViewModel group
                && DataContext is RoofDrainCalloutPlacingViewModel vm)
            {
                vm.SelectNoneInGroup(group);
            }
        }

        /// <summary>
        /// "Auto" toggle button in a group's sizing row. DataContext here is the
        /// GroupSizingViewModel (set via DataContext="{Binding Sizing}" in XAML).
        /// Clicking Auto always sets IsAutoMode true, mirroring radio-button
        /// mutual-exclusivity without needing a RadioButton GroupName per group.
        /// </summary>
        private void GroupSizingModeAuto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is GroupSizingViewModel sizing)
            {
                sizing.IsAutoMode = true;
            }
        }

        /// <summary>
        /// "Fixed" toggle button in a group's sizing row — see GroupSizingModeAuto_Click.
        /// </summary>
        private void GroupSizingModeFixed_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is GroupSizingViewModel sizing)
            {
                sizing.IsAutoMode = false;
            }
        }

        /// <summary>
        /// Copy all selected log entries to clipboard.
        /// </summary>
        private void CopySelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (LogListBox.SelectedItems.Count == 0)
            {
                MessageBox.Show("No log entries selected.", "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var text = string.Join(Environment.NewLine, LogListBox.SelectedItems);
            Clipboard.SetText(text);
        }

        /// <summary>
        /// On window close, persist settings — per-group sizing (Auto/Fixed, margin,
        /// fixed size) for Circle/Rectangle/Other, plus drafting view selection.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (DataContext is RoofDrainCalloutPlacingViewModel vm)
            {
                var settingsService = new SettingsService();
                var groupSizing = vm.Groups.ToDictionary(g => g.GroupKey, g => g.Sizing.ToSettings());

                settingsService.SaveSettings(new RoofDrainCalloutSettings
                {
                    GroupSizing = groupSizing,
                    DraftingViewName = vm.SelectedDraftingView?.Name ?? "",
                    LastRunSucceeded = vm.HasRun,
                    LastRunTimestamp = DateTime.Now.ToString("O")
                });
            }
        }
    }
}
