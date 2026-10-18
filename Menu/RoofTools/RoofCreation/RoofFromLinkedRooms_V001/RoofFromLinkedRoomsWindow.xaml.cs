using System.Linq;
using System.Text;
using System.Windows;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofFromLinkedRooms.V001
{
    /// <summary>Roof From Linked Rooms window (code-behind: close, settings save, log copy).</summary>
    public partial class RoofFromLinkedRoomsWindow : Window
    {
        /// <summary>Creates the window.</summary>
        public RoofFromLinkedRoomsWindow()
        {
            InitializeComponent();
            Closing += (s, e) => { if (DataContext is MainViewModel vm) vm.SaveSettings("window closed"); };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void CopySelectedLogs_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (LogListBox.SelectedItems.Count == 0)
            {
                vm?.ShowToast("No log lines selected");
                return;
            }

            var sb = new StringBuilder();
            foreach (var item in LogListBox.SelectedItems.Cast<LogEntry>())
                sb.AppendLine(item.ToString());
            Clipboard.SetText(sb.ToString());
            vm?.ShowToast("Selected log lines copied");
        }
    }
}
