using Autodesk.Revit.UI;
using System.Windows;
using Revit26_Plugin.DwgSymbolicConverter_V02.ViewModels;
using Revit26_Plugin.DwgSymbolicConverter_V02.Helpers;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Views
{
    public partial class DwgSymbolicConverterView : Window
    {
        public DwgSymbolicConverterView(UIApplication uiApp)
        {
            InitializeComponent();

            UiDispatcherHelper.Initialize(Dispatcher);

            DataContext =
                new DwgSymbolicConverterViewModel(uiApp, this);
        }
    }
}
