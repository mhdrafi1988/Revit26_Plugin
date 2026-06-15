using System.Windows;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V52.ViewModels;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V52.Views
{
    public partial class RoofRidgeView : Window
    {
        public RoofRidgeView(RoofRidgeViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
