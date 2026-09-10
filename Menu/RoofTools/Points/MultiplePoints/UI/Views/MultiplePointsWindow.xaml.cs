using Revit26_Plugin.MultiplePoints.V001.UI.ViewModels;
using System.Windows;

namespace Revit26_Plugin.MultiplePoints.V001.UI.Views
{
    public partial class MultiplePointsWindow : Window
    {
        public MultiplePointsWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // Auto-scroll the log to the newest entry as rows are added.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MultiplePointsViewModel vm)
                vm.LogEntries.CollectionChanged += (s, a) => LogScroll?.ScrollToEnd();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
