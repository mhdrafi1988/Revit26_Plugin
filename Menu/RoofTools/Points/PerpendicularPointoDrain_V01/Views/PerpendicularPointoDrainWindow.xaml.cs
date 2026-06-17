using Revit26_Plugin.PerpendicularPointoDrain.V01.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Revit26_Plugin.PerpendicularPointoDrain.V01.Views
{
    public partial class PerpendicularPointoDrainWindow : Window
    {
        public PerpendicularPointoDrainWindow()
        {
            InitializeComponent();
            DataContextChanged += PerpendicularPointoDrainWindow_DataContextChanged;
        }

        private void PerpendicularPointoDrainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PerpendicularPointoDrainViewModel oldVm)
            {
                oldVm.HideRequested -= OnHideRequested;
                oldVm.ShowRequested -= OnShowRequested;
            }

            if (e.NewValue is PerpendicularPointoDrainViewModel newVm)
            {
                newVm.HideRequested += OnHideRequested;
                newVm.ShowRequested += OnShowRequested;
            }
        }

        private void OnHideRequested()
        {
            // Dropping Topmost first avoids it briefly flashing back on top of the
            // Revit view during the picking operation on some window managers.
            Topmost = false;
            Hide();
        }

        private void OnShowRequested()
        {
            Show();
            Topmost = true;
            Activate();
        }

        /// <summary>
        /// Auto-scroll the log TextBox to the bottom whenever its text changes.
        /// </summary>
        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LogTextBox.ScrollToEnd();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
