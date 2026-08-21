using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.UI;
using Revit26_Plugin.LinkedDetailLineGenerator.VA003.UI.ViewModels;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.UI.Views
{
    /// <summary>
    /// Code-behind is intentionally thin — no Revit API or business logic here.
    /// Modeless window (Show(), not ShowDialog()) per suite convention; Esc closes it.
    /// Window never auto-closes on its own — only via explicit Close button or Esc.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(UIApplication uiApp)
        {
            InitializeComponent();

            var viewModel = new MainViewModel(uiApp);
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
