using Autodesk.Revit.UI;
using System.Windows;
using Revit26_Plugin.DwgToDetailLines.V002.ViewModels;

namespace Revit26_Plugin.DwgToDetailLines.V002.Views
{
    public partial class DwgToDetailLinesView : Window
    {
        public DwgToDetailLinesView(UIApplication uiApp)
        {
            InitializeComponent();
            DataContext = new DwgToDetailLinesViewModel(uiApp);
        }
    }
}
