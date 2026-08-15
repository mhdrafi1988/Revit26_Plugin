using Autodesk.Revit.UI;
using Revit26_Plugin.BubbleAutoRenumber.V006.Handlers;
using Revit26_Plugin.BubbleAutoRenumber.V006.ViewModels;
using System.Windows;
using System.Windows.Interop;

namespace Revit26_Plugin.BubbleAutoRenumber.V006.Views
{
    public partial class SectionAutoRenumberWindow : Window
    {
        public SectionAutoRenumberWindow(
            UIDocument                 uidoc,
            UIApplication              uiapp,
            SectionAutoRenumberHandler handler,
            ExternalEvent              externalEvent)
        {
            InitializeComponent();

            DataContext = new SectionAutoRenumberViewModel(uidoc, handler, externalEvent);

            new WindowInteropHelper(this)
            {
                Owner = uiapp.MainWindowHandle
            };
        }
    }
}
