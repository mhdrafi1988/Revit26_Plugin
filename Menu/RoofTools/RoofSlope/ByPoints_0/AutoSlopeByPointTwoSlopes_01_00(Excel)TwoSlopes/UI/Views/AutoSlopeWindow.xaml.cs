using Autodesk.Revit.UI;
using AutoSlopeByPointTwoSlopes_01_00.UI.ViewModels;
using System.Windows;

namespace AutoSlopeByPointTwoSlopes_01_00.UI.Views
{
    public partial class AutoSlopeWindow : Window
    {
        private AutoSlopeViewModel _viewModel;

        // Only one constructor — ViewModel is always created in AutoSlopeCommand
        // and passed in here. The old constructor that created its own ViewModel
        // has been removed because it cannot satisfy the new constructor signature
        // (VertexSelectionHandler + ExternalEvent must be created inside
        // IExternalCommand.Execute, not here).
        public AutoSlopeWindow(AutoSlopeViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _viewModel.SetParentWindow(this);
            DataContext = _viewModel;

            this.Focus();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_viewModel != null)
                _viewModel.ParentWindow = null;

            base.OnClosing(e);
        }
    }
}