using System.Windows;
using Revit26_Plugin.Creaser_adv_V001.ViewModels;

namespace Revit26_Plugin.Creaser_adv_V001.Views
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
