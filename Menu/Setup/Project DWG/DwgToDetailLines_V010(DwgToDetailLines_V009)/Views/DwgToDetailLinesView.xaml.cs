using Autodesk.Revit.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Revit26_Plugin.DwgToDetailLines.V010.ViewModels;

namespace Revit26_Plugin.DwgToDetailLines.V010.Views
{
    public partial class DwgToDetailLinesView : Window
    {
        public DwgToDetailLinesView(UIApplication uiApp)
        {
            InitializeComponent();
            DataContext = new DwgToDetailLinesViewModel(uiApp);
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
    }
}
