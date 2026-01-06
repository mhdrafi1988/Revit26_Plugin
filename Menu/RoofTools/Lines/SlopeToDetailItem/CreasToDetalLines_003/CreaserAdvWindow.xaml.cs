using System.Windows;
using Revit26_Plugin.CreaserAdv_V003.ViewModels;

namespace Revit26_Plugin.CreaserAdv_V003.Views
{
    public partial class CreaserAdvWindow : Window
    {
        public CreaserAdvWindow(CreaserAdvViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
