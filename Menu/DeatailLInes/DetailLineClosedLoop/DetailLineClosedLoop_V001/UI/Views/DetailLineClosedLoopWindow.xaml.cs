using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Revit26_Plugin.DetailLineClosedLoop.V001.UI.ViewModels;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.UI.Views
{
    public partial class DetailLineClosedLoopWindow : Window
    {
        public DetailLineClosedLoopWindow()
        {
            InitializeComponent();
            PreviewKeyDown += OnPreviewKeyDown;
            Closing += OnClosing;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is DetailLineClosedLoopViewModel vm)
                vm.OwnerWindow = this;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is DetailLineClosedLoopViewModel vm)
                vm.PersistSettings();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void CopySelectedLogsButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is DetailLineClosedLoopViewModel vm)
            {
                vm.SelectedLogItemsForCopy = LogListBox.SelectedItems;
                vm.CopySelectedLogsCommand.Execute(null);
            }
        }

        // ── Created Lines grid checkbox: block row-select cascade, per DataGrid spec ──
        private void CreatedLinesRowCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false; // allow the click to toggle the checkbox itself
            if (sender is DependencyObject d)
            {
                var row = FindParent<DataGridRow>(d);
                if (row != null)
                    row.IsSelected = false;
            }
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
                parent = VisualTreeHelper.GetParent(parent);
            return parent as T;
        }
    }
}
