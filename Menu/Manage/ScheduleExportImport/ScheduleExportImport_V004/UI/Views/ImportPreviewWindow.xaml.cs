using System.Windows;
using Revit26_Plugin.ScheduleExportImport.V004.UI.ViewModels;

namespace Revit26_Plugin.ScheduleExportImport.V004.UI.Views
{
    public partial class ImportPreviewWindow : Window
    {
        public ImportPreviewWindow(ScheduleExportImportViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
