using System.Windows;
using MahApps.Metro.Controls;

namespace Revit26_Plugin.RoofDetailLineIntersect.V005
{
    public partial class RoofDetailLineIntersectWindow : MetroWindow
    {
        public RoofDetailLineIntersectWindow(RoofDetailLineIntersectViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
