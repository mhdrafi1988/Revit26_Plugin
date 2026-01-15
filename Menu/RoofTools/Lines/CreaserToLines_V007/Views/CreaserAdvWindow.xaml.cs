using System.Windows;
using Revit26_Plugin.CreaserAdv_V00.ViewModels;

namespace Revit26_Plugin.CreaserAdv_V00.Views
{
    public partial class CreaserAdvWindow : Window
    {
        public CreaserAdvWindow(CreaserAdvViewModel viewModel)
        {
            InitializeComponent();

            // MVVM rule: ViewModel is injected, never created here
            DataContext = viewModel;
        }
    }
}
