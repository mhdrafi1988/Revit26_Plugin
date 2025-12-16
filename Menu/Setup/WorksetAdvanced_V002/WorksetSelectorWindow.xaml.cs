using Autodesk.Revit.UI;
using MahApps.Metro.Controls;
using Revit26_Plugin.WSAV02.ViewModels;
namespace Revit26_Plugin.WSAV02.Views
{
    public partial class WorksetSelectorWindow : MetroWindow
    {
        public WorksetSelectorWindow(ExternalCommandData commandData)
        {
            InitializeComponent();

            var vm = new WorksetsViewModel(commandData);
            vm.RequestClose += _ => this.Close();
            DataContext = vm;
        }
    }
}
