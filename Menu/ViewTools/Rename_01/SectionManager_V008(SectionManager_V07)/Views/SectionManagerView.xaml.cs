using System;
using System.Windows.Controls;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionManager.V008.Helpers;
using Revit26_Plugin.SectionManager.V008.ViewModels;

namespace Revit26_Plugin.SectionManager.V008.Views
{
    public partial class SectionManagerView : UserControl
    {
        private SectionManagerViewModel _viewModel;

        public SectionManagerView()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// ONLY place ViewModel is created
        /// </summary>
        public void Initialize(UIApplication uiApp)
        {
            if (!RevitContextGuard.HasActiveDocument(uiApp))
                return;

            _viewModel = new SectionManagerViewModel(uiApp);
            DataContext = _viewModel;
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel?.Dispose();
            _viewModel = null;
            DataContext = null;
        }
    }
}
