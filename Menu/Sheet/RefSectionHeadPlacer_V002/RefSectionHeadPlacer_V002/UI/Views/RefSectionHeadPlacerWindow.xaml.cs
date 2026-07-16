using System.Linq;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RefSectionHeadPlacer.V002.Core.Models;
using Revit26_Plugin.RefSectionHeadPlacer.V002.UI.ViewModels;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RefSectionHeadPlacer.V002.UI.Views
{
    public partial class RefSectionHeadPlacerWindow : Window
    {
        private readonly RefSectionHeadPlacerViewModel _viewModel;

        public RefSectionHeadPlacerWindow(UIApplication uiApp, Document doc)
        {
            InitializeComponent();

            _viewModel = new RefSectionHeadPlacerViewModel(doc);
            DataContext = _viewModel;

            // Window ownership via WindowInteropHelper — never Application.Current
            // .MainWindow in a Revit add-in.
            new WindowInteropHelper(this).Owner = uiApp.MainWindowHandle;

            // ViewModel raises CloseRequested (it has no window reference itself).
            _viewModel.CloseRequested += Close;

            // MEMORY: dispose the ViewModel when the window closes so it unsubscribes
            // from the long-lived ExternalEvent handler and disposes the event.
            Closed += (s, e) =>
            {
                _viewModel.CloseRequested -= Close;
                _viewModel.Dispose();
            };
        }

        /// <summary>
        /// Copy Selected — code-behind per convention: reads the log grid's
        /// SelectedItems directly rather than routing through the ViewModel.
        /// </summary>
        private void OnCopySelectedLogsClick(object sender, RoutedEventArgs e)
        {
            var text = string.Join(System.Environment.NewLine,
                GridLog.SelectedItems.Cast<LogEntry>().Select(entry => entry.ToString()));
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }
    }
}
