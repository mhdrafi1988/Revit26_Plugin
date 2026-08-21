using System.Linq;
using System.Text;
using System.Windows;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V004
{
    public partial class FloorsFromLinkedRoomsWindow : Window
    {
        public FloorsFromLinkedRoomsWindow()
        {
            InitializeComponent();
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = LogListBox.SelectedItems.Cast<LogEntry>().ToList();
            if (selected.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var entry in selected) sb.AppendLine(entry.ToString());
            Clipboard.SetText(sb.ToString());

            if (DataContext is MainViewModel vm) vm.ShowToast("Selected logs copied to clipboard");
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
