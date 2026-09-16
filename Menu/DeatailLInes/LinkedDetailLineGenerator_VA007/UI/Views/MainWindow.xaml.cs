using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.LinkedDetailLineGenerator.VA007.UI.ViewModels;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA007.UI.Views
{
    /// <summary>
    /// Code-behind is intentionally thin — no Revit API or business logic here.
    /// Modeless window (Show(), not ShowDialog()) per suite convention; Esc closes it.
    /// Window never auto-closes on its own — only via explicit Close button or Esc.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>selectedBoundaryElement is the Floor/Roof the command required
        /// the user to pre-select — it's the source of the "Restrict to boundary"
        /// clip shape and never changes for the lifetime of this window.</summary>
        public MainWindow(UIApplication uiApp, Element selectedBoundaryElement)
        {
            InitializeComponent();

            var viewModel = new MainViewModel(uiApp, selectedBoundaryElement);
            viewModel.SetOwnerWindow(this);
            DataContext = viewModel;

            // Never use Application.Current.MainWindow for Revit add-ins — use the
            // Revit main window handle via WindowInteropHelper (suite convention).
            new System.Windows.Interop.WindowInteropHelper(this).Owner = uiApp.MainWindowHandle;

            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
