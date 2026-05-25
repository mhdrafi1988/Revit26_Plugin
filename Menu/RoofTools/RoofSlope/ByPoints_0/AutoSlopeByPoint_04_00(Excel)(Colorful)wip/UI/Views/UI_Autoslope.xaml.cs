// =======================================================
// File: UI_Autoslope.xaml.cs
// Fixes:
//   #1  Removed unused Action<string> log parameter from
//       AutoSlopeViewModel constructor call.
//   #1  Removed empty AddLog method from code-behind.
//   #12 Removed empty ToggleButton_Checked handler that was
//       never wired to any control in the XAML.
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

            var viewModel = new AutoSlopeViewModel(uidoc, app, roofId, drains);
            DataContext = viewModel;
            this.Focus();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
        }
    }
}
