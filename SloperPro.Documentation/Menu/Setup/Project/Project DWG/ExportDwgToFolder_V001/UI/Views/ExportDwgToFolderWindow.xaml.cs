using System.Windows;
using Revit26_Plugin.ExportDwgToFolder.V001.UI.ViewModels;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.ExportDwgToFolder.V001.UI.Views
{
    public partial class ExportDwgToFolderWindow : Window
    {
        public ExportDwgToFolderWindow(ExportDwgToFolderViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void CopySelectedLog_Click(object sender, RoutedEventArgs e)
            => LogClipboardService.CopySelected(LogList.SelectedItems);
    }
}
