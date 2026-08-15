using Revit26_Plugin.WorksetManager.V011.ViewModels;
using System.Windows;

namespace Revit26_Plugin.WorksetManager.V011.Views
{
    // ── Window ────────────────────────────────────────────────────────────────

    public partial class WorksetSelectorWindow : Window
    {
        private readonly WorksetsViewModel _viewModel;

        public WorksetSelectorWindow(WorksetsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            viewModel.RequestClose += () => Close();
        }

        // "Copy selected" reads the ListBox's live selection — kept in code-behind
        // rather than a SelectedItems binding, since no selection-binding behavior
        // is set up elsewhere in this codebase yet.
        private void CopySelectedLog_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CopySelectedLog(LogListBox.SelectedItems);
        }
    }
}
