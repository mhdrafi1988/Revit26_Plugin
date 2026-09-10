using Revit26_Plugin.OuterCurveDivider.V004.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Revit26_Plugin.CombinedRoofTools.V001.UI.Views.Tabs
{
    public partial class OuterCurveDividerTabView : UserControl
    {
        public OuterCurveDividerTabView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // Auto-scroll the log to the newest entry as rows are added.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is CurveDividerViewModel vm)
                vm.LogEntries.CollectionChanged += (s, a) => LogScroll?.ScrollToEnd();
        }
    }
}
