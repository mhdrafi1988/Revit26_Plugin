using Autodesk.Revit.UI;
using RoofTagV3.ViewModels;
using RoofTagV3.Utilities;
using System.Windows;

namespace RoofTagV3.Views
{
    public partial class RoofTagWindow : Window
    {
        private readonly LiveLogger _logger;

        public RoofTagViewModel ViewModel => (RoofTagViewModel)DataContext;

        public RoofTagWindow(UIApplication uiApplication)
        {
            InitializeComponent();
            _logger = new LiveLogger();
            DataContext = new RoofTagViewModel(uiApplication, _logger);
        }

        private void OnOkClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}