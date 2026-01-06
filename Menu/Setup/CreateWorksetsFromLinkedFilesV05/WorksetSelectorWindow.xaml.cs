using Autodesk.Revit.UI;
using Revit26_Plugin.WSA_V05.ViewModels;
using System.Windows;

namespace Revit26_Plugin.WSA_V05.Views
{
    public partial class WorksetSelectorWindow : Window
    {
        public WorksetSelectorWindow(ExternalCommandData cmd)
        {
            InitializeComponent();

            var vm = new WorksetsViewModel(cmd);
            //vm.RequestClose += (sender, args) => Close(); // Added a lambda to handle the missing RequestClose event

            DataContext = vm;
        }
    }
}
