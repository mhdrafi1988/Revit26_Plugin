using System;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.copv2.ViewModels;

namespace Revit22_Plugin.copv2.Views
{
    public partial class CalloutViewWindow : Window
    {
        public CalloutViewWindow(UIApplication uiapp, Document doc)
        {
            // Ensure the correct namespace and class name for InitializeComponent
            this.InitializeComponent();

            UIDocument uidoc = uiapp.ActiveUIDocument;
            DataContext = new CalloutViewViewModel(uidoc, doc);

            // Attach Revit window
            var hwnd = uiapp.MainWindowHandle;
            new WindowInteropHelper(this) { Owner = hwnd };

            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
        }
    }
}
