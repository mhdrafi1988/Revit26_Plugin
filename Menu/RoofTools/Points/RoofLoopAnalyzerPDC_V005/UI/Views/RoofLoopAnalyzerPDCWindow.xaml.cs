// =======================================================
// File: RoofLoopAnalyzerPDCWindow.xaml.cs
// Location: UI/Views/
// Changes vs V004:
//   FIX  DataContext is dynamic vm -> DataContext is RoofLoopAnalyzerPDCViewModel vm.
//        The dynamic cast discarded compile-time type checking for no
//        reason — every other tool in the suite uses the strongly-typed
//        pattern.
//   REMOVED orphaned doc comment ("Auto-scroll the log TextBox...") that
//        no longer had a matching method beneath it.
//   ADDED Window_KeyDown for Esc-to-close (modeless dialog convention).
// =======================================================

using Revit26_Plugin.RoofLoopAnalyzerPDC.V005.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Revit26_Plugin.RoofLoopAnalyzerPDC.V005.UI.Views
{
    public partial class RoofLoopAnalyzerPDCWindow : Window
    {
        public RoofLoopAnalyzerPDCWindow()
        {
            InitializeComponent();
        }

        // ── Esc = Close (modeless dialog convention) ─────────────────────────
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>Keeps the ViewModel's selection in sync for "Copy Selected".</summary>
        private void LogList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is RoofLoopAnalyzerPDCViewModel vm && sender is ListBox list)
                vm.SelectedLogEntries = list.SelectedItems;
        }

        /// <summary>Window never auto-closes; on user close we persist settings and
        /// flush the log to disk (project window &amp; logging rules).</summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is RoofLoopAnalyzerPDCViewModel vm)
            {
                try { vm.SaveSettingsOnClose(); } catch { /* never block close */ }
                try { vm.AutoSaveLog(); }        catch { /* never block close */ }
            }
            base.OnClosing(e);
        }
    }
}
