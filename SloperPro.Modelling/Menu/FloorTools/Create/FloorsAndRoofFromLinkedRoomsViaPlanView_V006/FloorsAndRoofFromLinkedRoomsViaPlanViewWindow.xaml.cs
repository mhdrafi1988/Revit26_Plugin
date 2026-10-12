using System.Linq;
using System.Text;
using System.Windows;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V006
{
    public partial class FloorsFromLinkedRoomsWindow : Window
    {
        public FloorsFromLinkedRoomsWindow()
        {
            InitializeComponent();

            // settings auto-save on property change — no explicit save needed on close
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = LogListBox.SelectedItems.Cast<LogEntry>().ToList();
            if (selected.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var entry in selected) sb.AppendLine(entry.ToString());
            System.Windows.Clipboard.SetText(sb.ToString());

            if (DataContext is MainViewModel vm) vm.ShowToast("Selected logs copied to clipboard");
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
