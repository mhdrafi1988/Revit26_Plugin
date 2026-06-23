using System.Windows;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.V005.ViewModels;

namespace Revit26_Plugin.RoofFromFloor.V005.Views
{
    public partial class RoofFromFloorWindow : Window
    {
        public RoofFromFloorWindow(UIApplication uiApp)
        {
            InitializeComponent();
            DataContext = new RoofFromFloorViewModel(uiApp, this);
        }
    }
}
