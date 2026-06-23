using System.Windows;
using MahApps.Metro.Controls;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V64.ViewModels;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V64.Views
{
    /// <summary>
    /// Code-behind for the Roof Ridge (Voronoi) tool window. Contains no logic beyond
    /// wiring the view model as <see cref="FrameworkElement.DataContext"/> — all
    /// behavior lives in <see cref="RoofRidgeViewModel"/> per the project's MVVM convention.
    /// </summary>
    public partial class RoofRidgeView : MetroWindow
    {
        /// <summary>Initializes the window and binds it to <paramref name="viewModel"/>.</summary>
        /// <param name="viewModel">The view model driving this window.</param>
        public RoofRidgeView(RoofRidgeViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
