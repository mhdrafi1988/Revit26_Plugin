using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.callout.ViewModels;
using Revit22_Plugin.callout.Commands;
using Revit22_Plugin.callout.Helpers;
using Revit22_Plugin.callout.Models;

namespace Revit22_Plugin.callout.Views
{
    public partial class SectionViewWindow : Window
    {
        public SectionViewWindow(UIApplication uiapp, Document doc)
        {
            InitializeComponent();
            DataContext = new CalloutViewViewModel(doc);
            new WindowInteropHelper(this).Owner = uiapp.MainWindowHandle;
        }

        private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
    }
}
