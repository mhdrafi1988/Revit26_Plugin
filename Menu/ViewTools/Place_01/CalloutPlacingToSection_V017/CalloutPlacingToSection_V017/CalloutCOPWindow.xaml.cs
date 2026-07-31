using Autodesk.Revit.UI;
using System.Linq;
using System.Windows;
using Revit26_Plugin.CalloutCOP.V017.ViewModels;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.CalloutCOP.V017.Views
{
    public partial class CalloutCOPWindow : Window
    {
        public CalloutCOPWindow(ExternalCommandData data)
        {
            InitializeComponent();
            DataContext = new CalloutCOPViewModel(data);
        }

        // Clipboard access only - no Revit API call here, so no ExternalEvent is needed.
        private void CopySelectedLogs_Click(object sender, RoutedEventArgs e)
        {
            var selected = LogListBox.SelectedItems.Cast<LogEntry>().ToList();
            if (!selected.Any())
                return;

            var text = string.Join(System.Environment.NewLine, selected.Select(l => l.ToString()));
            Clipboard.SetText(text);
        }
    }
}
