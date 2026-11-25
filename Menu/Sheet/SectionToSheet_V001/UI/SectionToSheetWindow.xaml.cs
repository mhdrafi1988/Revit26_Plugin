using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;
using System.Linq;
using System.Windows;

namespace Revit26_Plugin.SectionToSheet.V001.UI
{
    /// <summary>Section To Sheet window (modal). Settings are saved when it closes.</summary>
    public partial class SectionToSheetWindow : Window
    {
        private readonly SectionToSheetViewModel _vm;

        /// <summary>Creates the window for the given ViewModel.</summary>
        public SectionToSheetWindow(SectionToSheetViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
            Title = ToolCatalog.SectionToSheet.Title;
            Shell.Version = "v" + ToolCatalog.SectionToSheet.Version;
            Closed += (_, _) => _vm.SaveSettings();
        }

        private void CopySelectedLog_Click(object sender, RoutedEventArgs e)
        {
            _vm.SelectedLogEntries = LogListBox.SelectedItems.Cast<LogEntry>().ToList();
            _vm.CopySelectedLogCommand.Execute(null);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
