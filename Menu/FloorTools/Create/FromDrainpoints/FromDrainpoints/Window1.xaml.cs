using Autodesk.Revit.UI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Revit26_Plugin.RoomToRoofUI
{
    public partial class RoomToRoofWindow : Window
    {
        public RoomToRoofWindow(UIApplication uiApp)
        {
            InitializeComponent();

            this.DataContext = new RoomToRoofViewModel(uiApp);

            this.Topmost = true;

            IntPtr revitHandle = uiApp.MainWindowHandle;
            WindowInteropHelper helper = new WindowInteropHelper(this);
            helper.Owner = revitHandle;
        }

        private void RoomGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = this.DataContext as RoomToRoofViewModel;
            if (vm == null) return;

            vm.SelectedRooms.Clear();
            foreach (var item in ((DataGrid)sender).SelectedItems)
            {
                if (item is RoomViewModel room)
                {
                    vm.SelectedRooms.Add(room);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}