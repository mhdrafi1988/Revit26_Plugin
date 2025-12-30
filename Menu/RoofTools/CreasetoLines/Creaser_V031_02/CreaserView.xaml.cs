using System.Windows;
using Autodesk.Revit.UI;
using Revit26_Plugin.Creaser_V31.ViewModels;

namespace Revit26_Plugin.Creaser_V31.Views
{
    public partial class CreaserView : Window
    {
        public CreaserView(UIDocument uiDoc)
        {
            InitializeComponent();

            // View owns DataContext creation (MVVM rule)
            DataContext = new CreaserViewModel(uiDoc);
        }
    }
}
