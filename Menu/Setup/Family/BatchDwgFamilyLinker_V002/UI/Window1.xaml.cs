using System.Windows;
using BatchDwgFamilyLinker.V002.ViewModels;
using Revit26_Plugin.Shared.Services;

namespace BatchDwgFamilyLinker.V002.UI
{
    public partial class BatchLinkWindow : Window
    {
        public BatchLinkWindow(BatchLinkViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void CopyAllLog_Click(object sender, RoutedEventArgs e) => LogClipboardService.CopyAll(new[] { LiveLogBox.Text });

        private void CopySelectedLog_Click(object sender, RoutedEventArgs e) => LogClipboardService.CopyAll(new[] { LiveLogBox.SelectedText });
    }
}
