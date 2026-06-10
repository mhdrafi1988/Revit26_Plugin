using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Revit26_Plugin.WorksetManager_02.Models;
using Revit26_Plugin.WorksetManager_02.ViewModels;

namespace Revit26_Plugin.WorksetManager_02.Views
{
    public partial class WorksetManagerWindow : Window
    {
        // Tracks the last row index clicked per grid — used for Shift+Click range
        private int _lastClickedIndexGrid2 = -1;
        private int _lastClickedIndexGrid3 = -1;

        public WorksetManagerWindow(Document doc)
        {
            InitializeComponent();
            DataContext = new WorksetManagerViewModel(doc);
        }

        // ═══════════════════════════════════════════════════════════════════
        // GRID 2
        // ═══════════════════════════════════════════════════════════════════

        // ── Space key: toggle IsChecked on all highlighted rows ─────────────
        private void Grid2_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space) return;
            e.Handled = true;

            foreach (var item in Grid2.SelectedItems)
                if (item is LinkWorksetMatchItem m)
                    m.IsChecked = !m.IsChecked;
        }

        // ── Mouse click on row: handle Shift / Ctrl / plain click ───────────
        private void Grid2_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Find the row that was clicked
            DataGridRow? row = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
            if (row == null) return;

            // If the click landed directly on the CheckBox, let it pass through
            // so the checkbox itself handles it natively
            if (FindAncestor<CheckBox>((DependencyObject)e.OriginalSource) != null) return;

            if (row.Item is not LinkWorksetMatchItem clickedItem) return;

            int clickedIndex = Grid2.Items.IndexOf(clickedItem);
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool ctrl  = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            if (shift && _lastClickedIndexGrid2 >= 0)
            {
                // Shift+Click — set entire range to same state as the clicked row
                bool targetState = !clickedItem.IsChecked; // the row's state BEFORE toggle
                int from = System.Math.Min(_lastClickedIndexGrid2, clickedIndex);
                int to   = System.Math.Max(_lastClickedIndexGrid2, clickedIndex);

                for (int i = from; i <= to; i++)
                    if (Grid2.Items[i] is LinkWorksetMatchItem rangeItem)
                        rangeItem.IsChecked = targetState;
            }
            else
            {
                // Plain click or Ctrl+Click — toggle just this row's checkbox
                clickedItem.IsChecked = !clickedItem.IsChecked;
                _lastClickedIndexGrid2 = clickedIndex;
            }

            // Do NOT mark e.Handled — let WPF update row highlight normally
        }

        // ═══════════════════════════════════════════════════════════════════
        // GRID 3
        // ═══════════════════════════════════════════════════════════════════

        // ── Space key: toggle IsChecked on all highlighted rows ─────────────
        private void Grid3_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space) return;
            e.Handled = true;

            foreach (var item in Grid3.SelectedItems)
                if (item is UnmatchedLinkItem m)
                    m.IsChecked = !m.IsChecked;
        }

        // ── Mouse click on row: handle Shift / Ctrl / plain click ───────────
        private void Grid3_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataGridRow? row = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
            if (row == null) return;

            if (FindAncestor<CheckBox>((DependencyObject)e.OriginalSource) != null) return;

            if (row.Item is not UnmatchedLinkItem clickedItem) return;

            int clickedIndex = Grid3.Items.IndexOf(clickedItem);
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool ctrl  = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            if (shift && _lastClickedIndexGrid3 >= 0)
            {
                bool targetState = !clickedItem.IsChecked;
                int from = System.Math.Min(_lastClickedIndexGrid3, clickedIndex);
                int to   = System.Math.Max(_lastClickedIndexGrid3, clickedIndex);

                for (int i = from; i <= to; i++)
                    if (Grid3.Items[i] is UnmatchedLinkItem rangeItem)
                        rangeItem.IsChecked = targetState;
            }
            else
            {
                clickedItem.IsChecked = !clickedItem.IsChecked;
                _lastClickedIndexGrid3 = clickedIndex;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Helper — walk the visual tree upward to find a parent of type T
        // ═══════════════════════════════════════════════════════════════════
        private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
        {
            while (obj != null)
            {
                if (obj is T match) return match;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }
    }
}
