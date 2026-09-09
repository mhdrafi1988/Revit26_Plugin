// ==============================================
// File: DwgToDetailLinesWindow.xaml.cs
// Layer: UI/Views
// ==============================================

using Autodesk.Revit.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Revit26_Plugin.DwgToDetailLines.V011.UI.ViewModels;

namespace Revit26_Plugin.DwgToDetailLines.V011.UI.Views
{
    public partial class DwgToDetailLinesWindow : Window
    {
        public DwgToDetailLinesWindow(UIApplication uiApp)
        {
            InitializeComponent();

            var vm = new DwgToDetailLinesViewModel(uiApp);
            vm.RequestClose += Close;
            DataContext = vm;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        /// <summary>
        /// Per DATAGRID SPEC: checkbox clicks toggle the row's IsSelected
        /// binding but must not cascade into DataGridRow selection (which
        /// would fight the row's own selection/highlight state).
        /// </summary>
        private void LayerCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is CheckBox cb)
                cb.IsChecked = !(cb.IsChecked ?? false);

            e.Handled = true;
        }

        private void LogList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is DwgToDetailLinesViewModel vm && sender is ListBox list)
                vm.SelectedLogEntries = list.SelectedItems;
        }
    }
}
