// ==============================================
// File: WorksetCreatorWindow.xaml.cs
// Layer: UI/Views
// Workset Creator V017 — unchanged apart from the namespace.
// "Copy Selected" feeds the shared base's SelectedLogEntries and runs its
// CopySelectedLogCommand.
// ==============================================

using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.WorksetCreator.V017.UI.ViewModels;
using System.Linq;
using System.Windows;

namespace Revit26_Plugin.WorksetCreator.V017.UI.Views
{
    public partial class WorksetCreatorWindow : Window
    {
        private readonly WorksetCreatorViewModel _viewModel;

        public WorksetCreatorWindow(WorksetCreatorViewModel viewModel)
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
            _viewModel.SelectedLogEntries = LogListBox.SelectedItems
                .Cast<LogEntry>()
                .ToList();

            _viewModel.CopySelectedLogCommand.Execute(null);
        }
    }
}
