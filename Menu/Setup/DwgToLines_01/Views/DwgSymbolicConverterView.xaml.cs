using Autodesk.Revit.UI;
using System.Windows;
using Revit26_Plugin.DwgSymbolicConverter_V01.ViewModels;

namespace Revit26_Plugin.DwgSymbolicConverter_V01.Views
{
    public partial class DwgSymbolicConverterView : Window
    {
        public DwgSymbolicConverterView(UIApplication uiApp)
        {
            InitializeComponent();

            // MVVM-only wiring
            DataContext = new DwgSymbolicConverterViewModel(uiApp);
        }
    }
}
