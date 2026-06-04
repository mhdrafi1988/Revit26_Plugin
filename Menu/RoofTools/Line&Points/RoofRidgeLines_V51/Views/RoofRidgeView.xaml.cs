using System.Windows;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V51.ViewModels;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V51.Views
{
    /// <summary>
    /// Code-behind for RoofRidgeView.xaml.
    /// Contains only the constructor — all logic lives in RoofRidgeViewModel.
    /// </summary>
    public partial class RoofRidgeView : Window
    {
        public RoofRidgeView(RoofRidgeViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
