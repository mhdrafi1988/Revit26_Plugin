using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByDrain_06_00.Core.Models;
using Revit26_Plugin.AutoSlopeByDrain_06_00.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Revit26_Plugin.AutoSlopeByDrain_06_00.UI.Views
{
    public partial class AutoSlopeWindow : Window
    {
        private AutoSlopeViewModel _viewModel;

        public AutoSlopeWindow(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<DrainItem> detectedDrains)
        {
            InitializeComponent();

            _viewModel = new AutoSlopeViewModel(
                uidoc, app, roofId, detectedDrains, AddLog);

            _viewModel.CloseWindow = () => this.Close();
            DataContext = _viewModel;

            // Set window owner to Revit main window
            this.Loaded += (s, e) =>
            {
                if (System.Windows.Interop.ComponentDispatcher.IsThreadModal)
                {
                    this.Topmost = true;
                }
            };

            // Handle closing event
            this.Closing += OnWindowClosing;

            // Focus on window
            this.Focus();
        }

        private void AddLog(string message)
        {
            // This is handled by the ViewModel
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            // Check if operation is in progress
            if (_viewModel != null && _viewModel.IsProcessing)
            {
                var result = MessageBox.Show(
                    "Operation is still in progress. Are you sure you want to close?",
                    "Confirm Close",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // Cleanup
            _viewModel?.AddLog("Window closed");
        }

        // Keyboard shortcuts
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape)
            {
                if (_viewModel != null && !_viewModel.IsProcessing)
                {
                    _viewModel.CancelCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.F5)
            {
                // Refresh drains
                _viewModel?.RefreshDrainsCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+Enter to apply slopes
                _viewModel?.ApplySlopesCommand.Execute(null);
                e.Handled = true;
            }
        }

        // Ensure proper disposal
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_viewModel != null)
            {
                _viewModel.CloseWindow = null;
                _viewModel = null;
            }
        }
    }
}