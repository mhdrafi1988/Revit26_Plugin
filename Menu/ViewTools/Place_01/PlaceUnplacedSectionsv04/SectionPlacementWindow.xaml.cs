using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;

namespace Revit22_Plugin.SectionManagerMVVMv4
{
    public partial class SectionPlacementWindow : Window
    {
        public SectionPlacementWindow()
        {
            InitializeComponent();
        }

        // Pass these back to the command
        public IList<ViewSection> SelectedSections =>
            SectionListBox.SelectedItems.Cast<ViewSection>().ToList();

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSections.Count == 0)
            {
                MessageBox.Show(
                    "Please select at least one section view.",
                    "Validation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
