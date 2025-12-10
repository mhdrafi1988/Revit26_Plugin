using System.Windows;
using Revit22_Plugin.Asd_V4_01.ViewModels;

namespace Revit22_Plugin.Asd_V4_01.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(RoofSlopeMainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            vm.CloseWindow = () => this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
