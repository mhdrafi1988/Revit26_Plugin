using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Revit26_Plugin.PlanFromScopeBox.V001.UI.ViewModels;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.PlanFromScopeBox.V001.UI.Views
{
    /// <summary>
    /// Modeless window for the Plan From Scope Box tool. Parented to the Revit main window.
    /// Auto-scrolls the activity log, pushes log selection to the VM for Copy Selected, and
    /// saves settings on close.
    /// </summary>
    public partial class PlanFromScopeBoxWindow : Window
    {
        private readonly PlanFromScopeBoxViewModel _vm;

        public PlanFromScopeBoxWindow(UIApplication uiApp)
        {
            InitializeComponent();

            _vm = new PlanFromScopeBoxViewModel(uiApp);
            DataContext = _vm;

            new WindowInteropHelper(this).Owner = uiApp.MainWindowHandle;

            _vm.LogEntries.CollectionChanged += OnLogChanged;

            Closed += (_, __) =>
            {
                _vm.LogEntries.CollectionChanged -= OnLogChanged;
                _vm.Dispose();   // saves settings + disposes external event
            };
        }

        private void OnLogChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add) return;
            if (LogList.Items.Count == 0) return;
            FindScrollViewer(LogList)?.ScrollToBottom();
        }

        private void LogList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _vm.SelectedLogEntries = LogList.SelectedItems.Cast<LogEntry>().ToList();
        }

        private static ScrollViewer FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer sv) return sv;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                ScrollViewer found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
                if (found != null) return found;
            }
            return null;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
