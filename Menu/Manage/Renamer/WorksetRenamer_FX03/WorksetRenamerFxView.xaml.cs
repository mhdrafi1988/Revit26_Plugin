using System.IO;
using System.Linq;
using System.Windows;
using Revit26_Plugin.WorksetRenamer.FX03.ViewModels;

namespace Revit26_Plugin.WorksetRenamer.FX03.Views
{
    public partial class WorksetRenamerFxView : Window
    {
        private readonly WorksetRenamerFxViewModel _viewModel;

        public WorksetRenamerFxView(WorksetRenamerFxViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var xlsx = files?.FirstOrDefault(f => Path.GetExtension(f).Equals(".xlsx", System.StringComparison.OrdinalIgnoreCase));
            if (xlsx != null)
                _viewModel.LoadExcelFile(xlsx);
        }
    }
}
