using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Revit26_Plugin.RoofViewFocus.V001.UI.ViewModels;

namespace Revit26_Plugin.RoofViewFocus.V001.UI.Views
{
    /// <summary>Modeless window. Never auto-closes; Esc / Close button close it.</summary>
    public partial class RoofViewFocusWindow : Window
    {
        private readonly RoofViewFocusViewModel _vm;

        public RoofViewFocusWindow(RoofViewFocusViewModel vm)
        {
            InitializeComponent();

            _vm = vm;
            DataContext = vm;
            vm.PickFolder = PickFolder;

            ((INotifyCollectionChanged)vm.Log).CollectionChanged += OnLogChanged;

            Closing += (_, _) => _vm.OnWindowClosing();
            Closed += (_, _) => ((INotifyCollectionChanged)_vm.Log).CollectionChanged -= OnLogChanged;
        }

        private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || LogList.Items.Count == 0) return;

            // Defer until the new row's container exists.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (LogList.Items.Count > 0)
                    LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
            }), DispatcherPriority.Background);
        }

        private string? PickFolder()
        {
            var dialog = new OpenFolderDialog { Title = "Select a folder for Roof View Focus logs" };
            return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
