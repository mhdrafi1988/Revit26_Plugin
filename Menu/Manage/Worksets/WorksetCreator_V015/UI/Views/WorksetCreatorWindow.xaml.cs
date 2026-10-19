// ==============================================
// File: WorksetCreatorWindow.xaml.cs
// Layer: UI/Views
// Workset Creator V015 — renamed from Workset Manager V014's
// WorksetSelectorWindow.
// Change vs V014: "Copy Selected" now feeds the shared base's
// SelectedLogEntries and runs its CopySelectedLogCommand, instead of the
// tool's own CopySelectedLog method.
// ==============================================

using Revit26_Plugin_Revised.Shared.Models;
using Revit26_Plugin_Revised.WorksetCreator.V015.UI.ViewModels;
using System.Linq;
using System.Windows;

namespace Revit26_Plugin_Revised.WorksetCreator.V015.UI.Views
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
