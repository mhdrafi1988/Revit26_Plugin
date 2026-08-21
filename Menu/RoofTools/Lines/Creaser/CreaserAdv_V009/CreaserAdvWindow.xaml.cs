// ==================================
// File: CreaserAdvWindow.xaml.cs
// Namespace: Revit26_Plugin.CreaserAdv_V008_00
// ==================================

using System.Windows;
using Revit26_Plugin.CreaserAdv.V009.ViewModels;

namespace Revit26_Plugin.CreaserAdv.V009.Views
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
