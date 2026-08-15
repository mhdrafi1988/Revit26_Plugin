using System.Linq;
using System.Text;
using System.Windows;
using Revit26_Plugin.RoomToRoofOrFloor.V002.UI.ViewModels;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoomToRoofOrFloor.V002.UI.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close(); // user-triggered only; window never auto-closes after a run
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Join("\n", _viewModel.Logs.Select(l => l.ToString()));
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = LogListBox.SelectedItems.Cast<LogEntry>().ToList();
            if (selected.Count == 0) return;

            var text = string.Join("\n", selected.Select(l => l.ToString()));
            Clipboard.SetText(text);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Logs.Clear();
        }
    }
}
