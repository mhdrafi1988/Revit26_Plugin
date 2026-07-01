using System.Windows;
using Revit26_Plugin.CreaserAdv_V002_01.ViewModels;

namespace Revit26_Plugin.CreaserAdv_V002_01.Views
{
    public partial class CreaserAdvWindow : Window
    {
        public CreaserAdvWindow(CreaserAdvViewModel viewModel)
        {
            InitializeComponent(); // Remove 'this.' to avoid ambiguity
            DataContext = viewModel;
        }
    }
}
