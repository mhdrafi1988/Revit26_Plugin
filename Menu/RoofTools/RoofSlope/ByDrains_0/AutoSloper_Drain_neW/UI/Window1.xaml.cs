// =======================================================
// File: Views/AutoSlopeWindow.xaml.cs
// Description: Code-behind for AutoSlopeWindow (thin)
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByDrain_21.ViewModels;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

// Add this alias to resolve ambiguity
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Views
{
    public partial class AutoSlopeWindow : Window
    {
        private readonly AutoSlopeMergedViewModel _viewModel;

        public AutoSlopeWindow(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drains)
        {
            InitializeComponent();

            _viewModel = new AutoSlopeMergedViewModel(uidoc, app, roofId, drains);
            DataContext = _viewModel;

            // Let the ViewModel close the window
            _viewModel.CloseWindow = () => this.Close();

            // Find LogTextBox by name and setup auto-scroll
            this.Loaded += (s, e) =>
            {
                // Use the aliased WpfTextBox type
                var logTextBox = FindName("LogTextBox") as WpfTextBox;
                if (logTextBox != null)
                {
                    _viewModel.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName == nameof(_viewModel.LogText))
                        {
                            logTextBox.Dispatcher.Invoke(() =>
                            {
                                logTextBox.ScrollToEnd();
                            });
                        }
                    };
                }
            };
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
        }
    }
}