using System.Windows;

namespace Revit22_Plugin.SectionPlacement
{
    public partial class SectionPlacementWindow : Window
    {
        public SectionPlacementWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is SectionPlacementViewModel vm)
            {
                vm.SelectedSheets.Clear();
                foreach (var item in SheetsListBox.SelectedItems)
                {
                    if (item is Autodesk.Revit.DB.ViewSheet sheet)
                        vm.SelectedSheets.Add(sheet);
                }
            }

            this.DialogResult = true;
            this.Close();
        }
    }
}
