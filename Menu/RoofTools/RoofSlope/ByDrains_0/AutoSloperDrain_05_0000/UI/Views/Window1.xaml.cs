using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlope.V5_00.UI.ViewModels;
using System.Collections.Generic;
using System.Windows;

namespace Revit26_Plugin.AutoSlope.V5_00.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(UIDocument uidoc, UIApplication app, ElementId roofId)
        {
            InitializeComponent();

            var viewModel = new MainViewModel(uidoc, app, roofId, AddLog);
            DataContext = viewModel;

            // Hook up close action
            viewModel.CloseWindow = () => this.Close();

            // Set focus
            this.Focus();
        }

        private void AddLog(string message)
        {
            // UI updates are handled through binding
            Dispatcher.Invoke(() => { });
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
        }
    }
}