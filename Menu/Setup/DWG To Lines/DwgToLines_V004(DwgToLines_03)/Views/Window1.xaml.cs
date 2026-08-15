using Autodesk.Revit.UI;
using System.Windows;
using Revit26_Plugin.DwgToLines.V004.Helpers;
using Revit26_Plugin.DwgToLines.V004.ViewModels;

namespace Revit26_Plugin.DwgToLines.V004.Views
{
    public partial class DwgSymbolicConverterView : Window
    {
        public DwgSymbolicConverterView(UIApplication uiApp)
        {
            InitializeComponent();

            UiDispatcherHelper.Initialize(Dispatcher);
            DataContext = new DwgSymbolicConverterViewModel(uiApp);
        }
    }
}
