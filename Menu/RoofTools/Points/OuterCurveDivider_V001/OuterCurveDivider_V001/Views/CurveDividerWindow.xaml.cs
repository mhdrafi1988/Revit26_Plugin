using Revit26_Plugin.OuterCurveDivider.V001.ViewModels;
using System.Windows;

namespace Revit26_Plugin.OuterCurveDivider.V001.Views
{
    public partial class CurveDividerWindow : Window
    {
        public CurveDividerWindow()
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

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
