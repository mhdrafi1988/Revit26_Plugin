// ==================================
// File: CreaserAdvWindow.xaml.cs
// Namespace: Revit26_Plugin.CreaserAdv_V003_01
// ==================================

using System.Windows;
using Revit26_Plugin.CreaserAdv_V003_01.ViewModels;

namespace Revit26_Plugin.CreaserAdv_V003_01.Views
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
