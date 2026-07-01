using Autodesk.Revit.UI;
using Revit26_Plugin.RoofFromFloor.V010.ViewModels;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace Revit26_Plugin.RoofFromFloor.V010.Views
{
    public partial class RoofFromFloorWindow : Window
    {
        public RoofFromFloorWindow(UIApplication app)
        {
            InitializeComponent();

            var vm = new RoofFromFloorViewModel(app, this);
            DataContext = vm;

            vm.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
        }

        private void LogEntries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            LogScroller.ScrollToBottom();
        }
    }
}
