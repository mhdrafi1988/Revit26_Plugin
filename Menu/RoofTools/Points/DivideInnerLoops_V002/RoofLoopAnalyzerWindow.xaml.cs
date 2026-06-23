using MahApps.Metro.Controls;
using System.Windows;
using Revit26_Plugin.Tools.DivideInnerLoops.V002.ViewModels;

namespace Revit26_Plugin.Tools.DivideInnerLoops.V002.Views
{
    /// <summary>
    /// Interaction logic for <see cref="RoofLoopAnalyzerWindow"/>.
    /// Minimal code-behind: only window initialization and per-group button handlers.
    /// All core behaviour lives in the view-model.
    /// </summary>
    public partial class RoofLoopAnalyzerWindow : MetroWindow
    {
        /// <summary>Creates the window and initialises its XAML content.</summary>
        public RoofLoopAnalyzerWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles "Select all" button click for a group header.
        /// Calls the view-model to select all loops in the clicked group.
        /// </summary>
        /// <param name="sender">The clicked button.</param>
        /// <param name="e">Event arguments.</param>
        private void SelectGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is string category)
            {
                var vm = DataContext as RoofLoopAnalyzerViewModel;
                vm?.SelectGroupLoops(category);
            }
        }

        /// <summary>
        /// Handles "Select none" button click for a group header.
        /// Calls the view-model to clear selection on all loops in the clicked group.
        /// </summary>
        /// <param name="sender">The clicked button.</param>
        /// <param name="e">Event arguments.</param>
        private void ClearGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag is string category)
            {
                var vm = DataContext as RoofLoopAnalyzerViewModel;
                vm?.ClearGroupLoops(category);
            }
        }
    }
}
