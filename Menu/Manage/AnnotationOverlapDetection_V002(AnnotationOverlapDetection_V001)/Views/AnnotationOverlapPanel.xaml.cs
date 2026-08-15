using System.Windows;
using Revit26_Plugin.AnnotationOverlapDetection.V002.ViewModels;

namespace Revit26_Plugin.AnnotationOverlapDetection.V002.Views
{
    public partial class AnnotationOverlapPanel : Window
    {
        public AnnotationOverlapPanel(AnnotationOverlapViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
