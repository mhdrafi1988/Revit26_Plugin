using Autodesk.Revit.UI;
using Revit26_Plugin.WSFL_008.ViewModels;
using Revit26_Plugin.APUS_V307.Views.AutoPlaceSectionsWindow
using 
using System.Windows;

namespace Revit26_Plugin.WSFL_008.Views
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
