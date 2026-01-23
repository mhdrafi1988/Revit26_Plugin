using MahApps.Metro.Controls;
using Revit26_Plugin.WSFL_008.ViewModels;
using System.Collections.Specialized;

namespace Revit26_Plugin.WSFL_008.Views
{
    public partial class WorksetSelectorWindow : MetroWindow
    {
        public WorksetSelectorWindow(WorksetsViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
            //viewModel.RequestClose += Close;
        }
    }
}
