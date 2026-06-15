using Autodesk.Revit.DB;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Revit26_Plugin.WorksetManager_02.Models;
using Revit26_Plugin.WorksetManager_02.ViewModels;

namespace Revit26_Plugin.WorksetManager_02.Views
{
    public partial class WorksetManagerWindow : Window
    {
        public WorksetManagerWindow(Document doc)
        {
            InitializeComponent();
            DataContext = new WorksetManagerViewModel(doc);
        }

        // ── Grid 2 — Space key toggles IsChecked on all selected rows ──────

        private void Grid2_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space) return;
            e.Handled = true;

            if (sender is not DataGrid dg) return;

            foreach (var item in dg.SelectedItems)
            {
                if (item is LinkWorksetMatchItem m)
                    m.IsChecked = !m.IsChecked;
            }
        }

        // ── Grid 3 — Space key toggles IsChecked on all selected rows ──────

        private void Grid3_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space) return;
            e.Handled = true;

            if (sender is not DataGrid dg) return;

            foreach (var item in dg.SelectedItems)
            {
                if (item is UnmatchedLinkItem m)
                    m.IsChecked = !m.IsChecked;
            }
        }
    }
}
