using System.Windows;
using System.Windows.Controls;

namespace Revit26_Plugin.SectionViewAutoTagger.V003
{
    public partial class SectionViewAutoTaggerWindow : Window
    {
        public SectionViewAutoTaggerWindow(SectionViewAutoTaggerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Closes the Section Views popover. StaysOpen="False" already
        /// closes it on outside-click, but the explicit Done button gives a
        /// clear, discoverable way to close it after checking boxes (which
        /// — unlike the single-select DraftingViewSearchBehavior pattern —
        /// does NOT auto-close the popover on each check).
        /// </summary>
        private void DonePopoverButton_Click(object sender, RoutedEventArgs e)
        {
            SectionViewPopoverToggle.IsChecked = false;
        }

        /// <summary>
        /// ListBox.SelectedItems has no XAML binding support, so selection is
        /// synced here into the ViewModel's SelectedLogEntries collection,
        /// used by CopySelectedCommand.
        /// </summary>
        private void LogListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not SectionViewAutoTaggerViewModel vm) return;
            if (sender is not ListBox listBox) return;

            vm.SelectedLogEntries.Clear();
            foreach (var item in listBox.SelectedItems)
            {
                if (item is Revit26_Plugin.Shared.Models.LogEntry entry)
                    vm.SelectedLogEntries.Add(entry);
            }
        }
    }
}
