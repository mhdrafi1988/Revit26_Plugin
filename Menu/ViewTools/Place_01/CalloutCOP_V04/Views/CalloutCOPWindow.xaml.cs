using Revit26_Plugin.CalloutCOP_V04.Services;
using Revit26_Plugin.CalloutCOP_V04.ViewModels;
using System.Windows;
using System.Windows.Interop;

namespace Revit26_Plugin.CalloutCOP_V04.Views
{
    public partial class CalloutCOPWindow : Window
    {
        public CalloutCOPWindow(RevitContextService context)
        {
            InitializeComponent();
            DataContext = new CalloutCOPViewModel(context);
            new WindowInteropHelper(this) { Owner = context.UiApp.MainWindowHandle };
        }
    }
}
