using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V004
{
    public partial class FloorsFromLinkedRoomsWindow : Window
    {
        public FloorsFromLinkedRoomsWindow()
        {
            InitializeComponent();
        }

        /// <summary>DATAGRID SPEC item 2: stops the checkbox click from bubbling into the
        /// DataGridRow's own selection handling (which would otherwise also change
        /// DataGrid.SelectedItem/SelectedRoom on every checkbox toggle). The click is
        /// consumed here and used to toggle the CheckBox directly, since marking the event
        /// Handled would otherwise prevent the CheckBox's own default toggle behavior too.</summary>
        private void RowCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox)
            {
                checkBox.IsChecked = !(checkBox.IsChecked ?? false);
                e.Handled = true;
            }
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
