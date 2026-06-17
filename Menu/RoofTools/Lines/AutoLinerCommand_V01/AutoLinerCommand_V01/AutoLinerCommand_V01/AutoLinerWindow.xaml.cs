using System.Windows;
using Revit26_Plugin.AutoLiner_V01.ViewModels;

namespace Revit26_Plugin.AutoLiner_V01.Views
{
    public partial class AutoLinerWindow : Window
    {
        public AutoLinerWindow(AutoLinerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
