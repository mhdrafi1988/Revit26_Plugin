using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows;
using Revit22_Plugin.RoofTagV4.ViewModels;
using Revit22_Plugin.RoofTagV4.Models;

namespace Revit22_Plugin.RoofTagV4.Views
{
    public partial class RoofTagWindowV4 : Window
    {
        private readonly UIApplication _uiApp;
        private readonly RoofBase _roof;
        private readonly RoofLoopsModel _geom;

        public RoofTagWindowV4(UIApplication uiApp, RoofBase roof, RoofLoopsModel geom)
        {
            InitializeComponent();

            _uiApp = uiApp;
            _roof = roof;
            _geom = geom;

            // Load ViewModel with selected roof + pre-extracted geometry
            DataContext = new RoofTagViewModelV4(uiApp.ActiveUIDocument, roof, geom);
        }

        // --------------------------------------------------------------------
        // RUN BUTTON
        // --------------------------------------------------------------------
        private void OnOK(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as RoofTagViewModelV4;
            if (vm == null) return;

            // Execute full tagging process (does NOT close the window)
            RoofTagExecutionV4.Execute(_uiApp, vm, _roof, _geom);

            // Update UI with results
            vm.ResultMessage =
                $"✔ Success: {vm.SuccessCount}   ✘ Failed: {vm.FailCount}";
        }

        // --------------------------------------------------------------------
        // CLOSE BUTTON
        // --------------------------------------------------------------------
        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
