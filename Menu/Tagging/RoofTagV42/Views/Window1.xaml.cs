using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26.RoofTagV42.ViewModels;
using Revit26.RoofTagV42.Utilities;
using System;
using System.Windows;
using System.Windows.Threading;

namespace Revit26.RoofTagV42.Views
{
    public partial class RoofTagWindow : Window
    {
        private readonly ILiveLogger _logger;
        private readonly UIApplication _uiApplication;
        private readonly RoofBase _selectedRoof;
        private bool _isProcessing = false;

        public RoofTagViewModel ViewModel { get; private set; }

        public RoofTagWindow(UIApplication uiApplication, RoofBase selectedRoof)
        {
            InitializeComponent();
            _uiApplication = uiApplication;
            _selectedRoof = selectedRoof;
            _logger = new LiveLogger();

            // Initialize view model with Community Toolkit
            ViewModel = new RoofTagViewModel(uiApplication, _logger, selectedRoof);
            DataContext = ViewModel;

            // Set window properties
            ConfigureWindow();

            // Event handlers
            Loaded += OnWindowLoaded;
        }

        private void ConfigureWindow()
        {
            ShowInTaskbar = true;
            Topmost = false;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;

            // Set owner to Revit window
            if (_uiApplication.MainWindowHandle != IntPtr.Zero)
            {
                var interopHelper = new System.Windows.Interop.WindowInteropHelper(this);
                interopHelper.Owner = _uiApplication.MainWindowHandle;
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            ViewModel.LogInfo("✓ Roof Tag window loaded");
            ViewModel.LogInfo($"✓ Connected to document: {_uiApplication.ActiveUIDocument?.Document?.Title ?? "Unknown"}");

            if (_selectedRoof != null)
            {
                ViewModel.LogInfo($"✓ Roof ready for tagging: Element ID {_selectedRoof.Id}");
            }
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsProcessing)
            {
                ViewModel.LogWarning("⚠ Operation in progress. Please wait...");
                return;
            }

            Close();
        }

        // Remove the 'private' modifier or change to public
        public void CopyLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(ViewModel.LogText))
                {
                    Clipboard.SetText(ViewModel.LogText);
                    ViewModel.LogInfo("✓ Log copied to clipboard");
                }
                else
                {
                    ViewModel.LogWarning("⚠ Log is empty, nothing to copy");
                }
            }
            catch (Exception ex)
            {
                ViewModel.LogError($"Failed to copy log: {ex.Message}");
            }
        }

        public void UpdateLog(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateLog(message));
                return;
            }

            try
            {
                ViewModel.Log(message);

                if (ViewModel.AutoScroll && LogTextBox != null)
                {
                    LogTextBox.ScrollToEnd();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Log update failed: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}