using System.Linq;
using System.Text;
using System.Windows;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofFromDetailLines.V007
{
    public partial class RoofFromDetailLinesWindow : Window
    {
        public RoofFromDetailLinesWindow()
        {
            InitializeComponent();
            Closing += RoofFromDetailLinesWindow_Closing;
        }

        // Window stays open after Run completes (per confirmed spec) — Close is the
        // only way out, matching the suite's modeless dialog convention.
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void RoofFromDetailLinesWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.SaveSettings("window closed");
        }

        // Copy Selected is a UI/selection concern handled in code-behind, per suite convention.
        private void CopySelectedLogs_Click(object sender, RoutedEventArgs e)
        {
            if (LogListBox.SelectedItems.Count == 0)
            {
                if (DataContext is MainViewModel vmNone) vmNone.ShowToast("No log lines selected");
                return;
            }

            var sb = new StringBuilder();
            foreach (var item in LogListBox.SelectedItems.Cast<LogEntry>())
                sb.AppendLine(item.ToString());

            Clipboard.SetText(sb.ToString());

            if (DataContext is MainViewModel vm) vm.ShowToast("Selected log lines copied");
        }
    }
}
