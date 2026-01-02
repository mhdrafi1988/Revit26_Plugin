using System.Windows;
using Revit26_Plugin.CreaserAdv_V002.ViewModels;

namespace Revit26_Plugin.CreaserAdv_V002.Views
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
