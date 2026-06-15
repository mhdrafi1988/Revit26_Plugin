using System.Windows;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V53.ViewModels;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V53.Views
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
