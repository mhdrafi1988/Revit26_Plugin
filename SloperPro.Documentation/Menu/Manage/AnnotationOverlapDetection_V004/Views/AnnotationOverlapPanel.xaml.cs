using System.Windows;
using Revit26_Plugin.AnnotationOverlapDetection.V004.ViewModels;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.Views
{
    public partial class AnnotationOverlapPanel : Window
    {
        public AnnotationOverlapPanel(AnnotationOverlapViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
