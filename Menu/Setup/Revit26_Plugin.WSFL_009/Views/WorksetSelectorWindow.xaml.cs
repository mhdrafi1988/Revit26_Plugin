using MahApps.Metro.Controls;
using Revit26_Plugin.WSFL_009.ViewModels;

namespace Revit26_Plugin.WSFL_009.Views
{
    public partial class WorksetSelectorWindow : MetroWindow
    {
        public WorksetSelectorWindow(WorksetsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose += Close;
        }
    }
}