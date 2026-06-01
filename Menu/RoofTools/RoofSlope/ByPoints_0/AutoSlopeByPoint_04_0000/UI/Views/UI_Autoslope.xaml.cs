// =======================================================
// File: UI_Autoslope.xaml.cs
// Fixes:
//   #1  Removed unused Action<string> log parameter from
//       AutoSlopeViewModel constructor call.
//   #1  Removed empty AddLog method from code-behind.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint_04.UI.ViewModels;
using System.Collections.Generic;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByPoint_04.UI.Views
{
    public partial class AutoSlopeWindow : Window
    {
        public AutoSlopeWindow(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drains)
        {
            InitializeComponent();

            // No log delegate — ViewModel handles its own logging via Dispatcher.
            var viewModel = new AutoSlopeViewModel(uidoc, app, roofId, drains);
            DataContext = viewModel;
            this.Focus();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
