using Autodesk.Revit.UI;
using System.Windows;
using Revit26_Plugin.CalloutCOP_V06.ViewModels;

namespace Revit26_Plugin.CalloutCOP_V06.Views
{
    public partial class CalloutCOPWindow : Window
    {
        public CalloutCOPWindow(ExternalCommandData data)
        {
            InitializeComponent();
            DataContext = new CalloutCOPViewModel(data);
        }
    }
}
