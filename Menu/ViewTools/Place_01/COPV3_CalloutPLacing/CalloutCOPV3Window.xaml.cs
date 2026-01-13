using Autodesk.Revit.UI;
using System;
using System.Windows;
using System.Windows.Interop;

namespace Revit22_Plugin.copv3.Views
{
    public partial class CalloutCOPV3Window : Window
    {
        public CalloutCOPV3Window(UIApplication uiapp, UIDocument uidoc)
        {
            InitializeComponent();
            DataContext = new ViewModels.CalloutCOPV3ViewModel(uiapp, uidoc);

            var handle = uiapp.MainWindowHandle;
            new WindowInteropHelper(this) { Owner = handle };
        }
    }
}
