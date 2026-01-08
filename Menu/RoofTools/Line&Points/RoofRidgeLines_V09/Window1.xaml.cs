using System.Windows;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08
{
    public partial class Window1 : Window
    {
        public Window1()
        {
            InitializeComponent();

            // Set window owner to Revit main window for proper modal behavior
            this.Owner = System.Windows.Application.Current?.MainWindow;
        }
    }
}