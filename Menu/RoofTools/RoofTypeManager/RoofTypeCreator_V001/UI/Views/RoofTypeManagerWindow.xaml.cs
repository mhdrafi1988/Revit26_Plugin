using System.Linq;
using System.Windows;
using System.Windows.Input;
using Revit26_Plugin.RoofTypeCreator.V001.Core.Models;
using Revit26_Plugin.RoofTypeCreator.V001.UI.ViewModels;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofTypeCreator.V001.UI.Views
{
    public partial class RoofTypeManagerWindow : Window
    {
        private RoofTypeManagerViewModel Vm => DataContext as RoofTypeManagerViewModel;

        public RoofTypeManagerWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Vm?.WindowLoadedCommand.Execute(null);

            // Auto-scroll log when new entries are added
            if (Vm != null)
            {
                Vm.LogEntries.CollectionChanged += (_, args) =>
                {
                    if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
                        && LogListBox.Items.Count > 0)
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                    }
                };
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            Vm?.Dispose();
            base.OnClosed(e);
        }

        private void CopySelectedBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = LogListBox.SelectedItems
                .OfType<LogEntry>()
                .Select(l => l.ToString());
            var text = string.Join(System.Environment.NewLine, selected);
            if (!string.IsNullOrEmpty(text))
            {
                try { System.Windows.Clipboard.SetText(text); }
                catch { /* clipboard locked */ }
            }
        }
    }
}
