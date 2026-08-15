using Autodesk.Revit.UI;
using System.Windows;
using Revit26_Plugin.DwgToDetailLines.V001.ViewModels;

namespace Revit26_Plugin.DwgToDetailLines.V001.Views
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
