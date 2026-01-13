using System;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.callout.ViewModels;

namespace Revit22_Plugin.callout.Views
{
    public partial class SectionViewWindow : Window
    {
        public SectionViewWindow(UIApplication uiapp, Document doc)
        {
            InitializeComponent();
            this.DataContext = new SectionViewViewModel(doc);

            // 👇 Attach Revit as owner
            var revitHandle = uiapp.MainWindowHandle; // No need to wrap in IntPtr
            new WindowInteropHelper(this).Owner = revitHandle;

            // Optional: bring window to front
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Topmost = true;
            this.Activate();
        }
    }
}
