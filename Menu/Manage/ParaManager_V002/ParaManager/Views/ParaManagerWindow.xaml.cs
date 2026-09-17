using Revit26_Plugin.ParaManager.V002.ViewModels;
using Revit26_Plugin.Shared.Models;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Revit26_Plugin.ParaManager.V002.Views
{
    /// <summary>
    /// Code-behind for the ParaManager modeless tool window.
    /// Per suite convention: window never auto-closes; only Close button /
    /// title-bar close ends it. State is persisted to settings on Closing.
    /// </summary>
    public partial class ParaManagerWindow : Window
    {
        private readonly ParaManagerViewModel _viewModel;

        public ParaManagerWindow(ParaManagerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        /// <summary>
        /// Leaves the click unhandled so the CheckBox toggles itself normally. The
        /// DataGridRow's own selection highlight may also change on the same click —
        /// that's cosmetic only (StandardDataGridRow styling) and does not affect
        /// ParameterAssignmentRow.IsSelected, which is bound to the checkbox directly.
        /// </summary>
        private void RowCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
        }

        private void LogListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.SelectedLogEntries.Clear();
            if (sender is not ListBox lb) return;
            foreach (var item in lb.SelectedItems)
            {
                if (item is LogEntry entry)
                    _viewModel.SelectedLogEntries.Add(entry);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _viewModel.OnWindowClosing();
        }
    }
}
