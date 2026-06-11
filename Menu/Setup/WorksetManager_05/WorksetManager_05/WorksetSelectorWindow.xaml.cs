using Revit26_Plugin.WorksetManager_05.ViewModels;

namespace Revit26_Plugin.WorksetManager_05.Views
{
    public partial class WorksetSelectorWindow : MahApps.Metro.Controls.MetroWindow
    {
        public WorksetSelectorWindow(WorksetsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose += () => Close();
        }
    }
}
