using System;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoLiner_V02.ViewModels;

namespace Revit26_Plugin.AutoLiner_V02.Views
{
    public partial class AutoLinerWindow : Window
    {
        public AutoLinerWindow(MainViewModel vm, UIApplication uiApp)
        {
            InitializeComponent();

            DataContext = vm;

            // Attach window to Revit
            IntPtr revitHandle = uiApp.MainWindowHandle;
            new WindowInteropHelper(this).Owner = revitHandle;

            // Keep UI on top of Revit
            Topmost = true;
        }
    }
}
