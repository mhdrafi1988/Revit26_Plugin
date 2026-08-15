using System.Windows;
using Revit26_Plugin.RoofDrainCalloutPlacing.V005.ViewModels;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V005.Views
{
    public partial class RoofDrainCalloutPlacingWindow : Window
    {
        public RoofDrainCalloutPlacingWindow(RoofDrainCalloutPlacingViewModel viewModel)
        {
            // NOTE: SharedStyles.xaml is merged in XAML using the assembly-relative
            // pack URI form ("/Revit26_Plugin;component/Shared/SharedStyles.xaml").
            // V001 used pack://application:,,,/... which resolves against
            // System.Windows.Application.Current — that object doesn't exist in a
            // Revit add-in host process, so it threw on window load. The
            // assembly-relative form resolves against the loaded assembly directly
            // and needs no Application instance, which is why it's required here.
            InitializeComponent();

            DataContext = viewModel;

            Closing += (s, e) => viewModel.SaveSettings();
        }

        private void CopySelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is RoofDrainCalloutPlacingViewModel vm)
                vm.CopySelectedLogs(LogListBox.SelectedItems);
        }
    }
}
