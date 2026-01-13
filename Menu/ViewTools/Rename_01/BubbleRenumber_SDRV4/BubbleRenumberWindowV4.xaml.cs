using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using MahApps.Metro.Controls;
using Revit26_Plugin.SDRV4.ViewModels;

namespace Revit26_Plugin.SDRV4.Views
{
    public partial class BubbleRenumberWindowV4 : MetroWindow
    {
        public BubbleRenumberWindowV4(UIDocument uidoc, UIApplication uiapp)
        {
            InitializeComponent();

            // Attach ViewModel
            DataContext = new BubbleRenumberViewModelV4(uidoc);

            // Attach Revit main window to this dialog
            new WindowInteropHelper(this)
            {
                Owner = uiapp.MainWindowHandle
            };
        }
    }
}
