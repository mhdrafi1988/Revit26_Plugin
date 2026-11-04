using System.Windows;
using Revit26_Plugin.AnnotationOverlapDetection.V004.ViewModels;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.Views
{
    /// <summary>Batch picker window (v4.2.0): choose sheets and/or views to check.</summary>
    public partial class BatchScopePicker : Window
    {
        /// <summary>Creates the picker for the given view model.</summary>
        public BatchScopePicker(BatchScopePickerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose += Close;
        }
    }
}
