// =======================================================
// File: UI_Autoslope.xaml.cs
// Fixes:
//   #1  Corrected namespace to match XAML (041)
//   #2  Removed unused Action<string> log parameter from
//       AutoSlopeViewModel constructor call.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint_04_01.UI.ViewModels;   // ensure ViewModel namespace matches
using System.Collections.Generic;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByPoint_04_01.UI.Views      // ✅ changed from _04 to _041
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
            // placeholder for any toggle logic
        }
    }
}