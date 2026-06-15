using System.Windows;
using Revit26_Plugin.WorksetRenamer_01.ViewModels;

namespace Revit26_Plugin.WorksetRenamer_01.Views
{
    public partial class WorksetRenamerView : Window
    {
        public WorksetRenamerView(WorksetRenamerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
