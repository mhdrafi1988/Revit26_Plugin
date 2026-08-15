using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Revit26_Plugin.SmartViewToSheetPlacer.V220.ViewModels;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V220.Views
{
    /// <summary>
    /// Code-behind is intentionally minimal — all behavior is driven by
    /// SmartViewToSheetPlacerViewModel via data binding and commands (MVVM).
    /// The window closes only when the ViewModel raises CloseRequested
    /// (wired in SmartViewToSheetPlacerCommand), the user hits Esc/Close/Cancel,
    /// or Windows closes it directly — Window_Closing below always saves
    /// settings regardless of which path triggered the close (V213 — new).
    /// </summary>
    public partial class SmartViewToSheetPlacerWindow : Window
    {
        public SmartViewToSheetPlacerWindow()
        {
            InitializeComponent();
            LoadSharedStyles();
        }

        /// <summary>
        /// V213 — defensive fallback only. The primary SharedStyles.xaml merge
        /// now happens statically in XAML (see Window.Resources), using an
        /// assembly-relative pack URI so the XAML compiler can resolve
        /// StaticResource references at build time — a runtime-only fix here
        /// cannot satisfy the compiler, which is why an earlier version of
        /// this file (relying on this method alone) failed to build.
        /// This method is kept in case the relative pack URI ever fails to
        /// resolve in some Revit deployment/runtime scenario: it checks
        /// whether SharedStyles is already merged and only re-adds it if not,
        /// so it is a safe no-op in the normal case.
        /// </summary>
        private void LoadSharedStyles()
        {
            try
            {
                bool alreadyMerged = Resources.MergedDictionaries
                    .Any(d => d.Source != null && d.Source.OriginalString.Contains("SharedStyles.xaml"));
                if (alreadyMerged)
                    return;

                string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                string sharedStylesPath = Path.Combine(asmDir, "Shared", "SharedStyles.xaml");

                var dictionary = new ResourceDictionary
                {
                    Source = new Uri(sharedStylesPath, UriKind.Absolute)
                };

                Resources.MergedDictionaries.Insert(0, dictionary);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not load shared styles: {ex.Message}\n\nThe window will display with default WPF styling.",
                    "SmartViewToSheetPlacer — Style Load Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// V213 — new. Ensures settings are persisted on any window close path
        /// (Close/Cancel button, Esc, or the OS close button), not only when
        /// NextToStage2 runs. Delegates to the ViewModel so the save logic and
        /// its try/catch stay in one place (Settings partial).
        /// </summary>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            (DataContext as SmartViewToSheetPlacerViewModel)?.OnWindowClosing();
        }
    }
}
