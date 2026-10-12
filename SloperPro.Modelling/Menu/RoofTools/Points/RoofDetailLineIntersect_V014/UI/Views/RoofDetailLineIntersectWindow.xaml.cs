using System.Windows;
using System.Windows.Input;
using Revit26_Plugin.RoofDetailLineIntersect.V014.UI.ViewModels;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.RoofDetailLineIntersect.V014.UI.Views
{
    public partial class RoofDetailLineIntersectWindow : Window
    {
        public RoofDetailLineIntersectWindow(RoofDetailLineIntersectViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            KeyDown += Window_KeyDown;
        }

        // ── Esc = Close (modeless dialog convention) ─────────────────────────
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void CopySelectedLog_Click(object sender, System.Windows.RoutedEventArgs e) => LogClipboardService.CopySelected(LogList.SelectedItems);
    }
}
