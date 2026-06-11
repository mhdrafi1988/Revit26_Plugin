using Autodesk.Revit.UI;
using Revit26_Plugin.SectionAutoRenumber.Handlers;
using Revit26_Plugin.SectionAutoRenumber.ViewModels;
using System.Windows;
using System.Windows.Interop;

namespace Revit26_Plugin.SectionAutoRenumber.Views
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
