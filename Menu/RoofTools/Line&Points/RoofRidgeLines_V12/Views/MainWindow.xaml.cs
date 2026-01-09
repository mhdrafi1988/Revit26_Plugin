using System.Windows;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.ViewModels;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(UIDocument uiDoc)
        {
            InitializeComponent();
            DataContext = new MainViewModel(uiDoc, this);
        }
    }
}
