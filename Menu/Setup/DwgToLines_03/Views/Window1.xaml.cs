using Autodesk.Revit.UI;
using System.Windows;
using Revit26_Plugin.DwgSymbolicConverter_V03.Helpers;
using Revit26_Plugin.DwgSymbolicConverter_V03.ViewModels;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Views
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
