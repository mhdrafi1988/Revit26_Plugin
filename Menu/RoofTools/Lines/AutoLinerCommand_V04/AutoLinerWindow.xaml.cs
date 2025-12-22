using System.Windows;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoLiner_V04.ViewModels;

namespace Revit26_Plugin.AutoLiner_V04.Views
{
    public partial class AutoLinerWindow : Window
    {
        public AutoLinerWindow(UIDocument uiDoc)
        {
            InitializeComponent();
            DataContext = new AutoLinerViewModel(uiDoc);
        }
    }
}
