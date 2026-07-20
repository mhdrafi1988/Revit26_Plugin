using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Revit26_Plugin.RoofCreateTest.V001.UI
{
    /// <summary>
    /// Modeless test window. Close-only (no OK/Cancel), Esc closes.
    /// Never auto-closes after the operation.
    /// </summary>
    public partial class RoofCreateTestWindow : Window
    {
        public RoofCreateTestWindow()
        {
            InitializeComponent();

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) Close();
            };

            // Auto-scroll log to newest entry
            Loaded += (s, e) =>
            {
                if (DataContext is RoofCreateTestViewModel vm)
                {
                    vm.LogEntries.CollectionChanged += OnLogChanged;
                }
            };
        }

        private void OnLogChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && LogListBox.Items.Count > 0)
            {
                LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        private void OnCopySelectedClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is RoofCreateTestViewModel vm)
            {
                vm.CopySelectedCommand.Execute(LogListBox.SelectedItems.Cast<object>().ToList());
            }
        }
    }
}
