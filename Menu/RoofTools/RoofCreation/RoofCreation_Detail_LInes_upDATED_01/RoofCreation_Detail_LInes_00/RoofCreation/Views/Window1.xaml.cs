using System.Windows;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.ViewModels;

namespace Revit26_Plugin.RoofFromFloor.Views
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
