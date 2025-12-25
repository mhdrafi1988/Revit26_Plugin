using System.Windows;
using Revit26_Plugin.Creaser_V100.ViewModels;

namespace Revit26_Plugin.Creaser_V100.Views
{
    public partial class RoofCreaserWindow : Window
    {
        public RoofCreaserWindow()
        {
            InitializeComponent();

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is RoofCreaserViewModel vm)
            {
                vm.RequestClose += () => Close();
            }
        }
    }
}
