using System.Windows;
using Revit26_Plugin.ScheduleExportImport.V001.UI.ViewModels;

namespace Revit26_Plugin.ScheduleExportImport.V001.UI.Views
{
    public partial class ScheduleExportImportWindow : Window
    {
        public ScheduleExportImportWindow(ScheduleExportImportViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
