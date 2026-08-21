using System.Windows;
using Revit26_Plugin.WorksetRenamer.V003.ViewModels;

namespace Revit26_Plugin.WorksetRenamer.V003.Views
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
