using System.Windows;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V54.ViewModels;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V54.Views
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
