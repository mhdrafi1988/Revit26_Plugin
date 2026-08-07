using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V008
{
    public partial class FloorsFromLinkedRoomsWindow : Window
    {
        /// <summary>Guards against the programmatic Text reset in DropDownClosed
        /// re-triggering the filter handler.</summary>
        private bool _suppressComboFilter;

        public FloorsFromLinkedRoomsWindow()
        {
            InitializeComponent();

            // V005: persist selections when the window closes (settings JSON spec).
            Closing += (s, e) =>
            {
                if (DataContext is MainViewModel vm) vm.SaveSettings("window closed");
            };
        }

        /// <summary>DATAGRID SPEC item 2: stops the checkbox click from bubbling into the
        /// DataGridRow's own selection handling (which would otherwise also change
        /// DataGrid.SelectedItem/SelectedRoom on every checkbox toggle). The click is
        /// consumed here and used to toggle the CheckBox directly.</summary>
        private void RowCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is CheckBox checkBox)
            {
                checkBox.IsChecked = !(checkBox.IsChecked ?? false);
                e.Handled = true;
            }
        }

        // ─── New Level live-filter combo (V005) ─────────────────────────────────
        // Each row's ComboBox gets its OWN ListCollectionView over the shared
        // HostLevels collection, so filtering one row never affects another (the
        // default CollectionView would be shared). Typing live-filters the dropdown
        // by substring; closing the dropdown clears the filter and restores the
        // selected item's text. Works with Recycling virtualization: on container
        // reuse the view already exists and the filter is simply cleared.

        private void NewLevelCombo_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ComboBox cb) return;
            if (DataContext is not MainViewModel vm) return;

            if (cb.ItemsSource is not ListCollectionView)
            {
                // Swapping ItemsSource can momentarily null the TwoWay SelectedItem
                // binding — capture and restore so the row's mapping is preserved.
                var current = cb.SelectedItem;
                cb.ItemsSource = new ListCollectionView(vm.HostLevels);
                cb.SelectedItem = current;

                cb.RemoveHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(NewLevelCombo_TextChanged));
                cb.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(NewLevelCombo_TextChanged));
                cb.DropDownOpened -= NewLevelCombo_DropDownOpened;
                cb.DropDownOpened += NewLevelCombo_DropDownOpened;
                cb.DropDownClosed -= NewLevelCombo_DropDownClosed;
                cb.DropDownClosed += NewLevelCombo_DropDownClosed;
            }
            else if (cb.ItemsSource is ListCollectionView view)
            {
                view.Filter = null; // recycled container: start clean for the new row
            }
        }

        private void NewLevelCombo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressComboFilter) return;
            if (sender is not ComboBox cb || cb.ItemsSource is not ListCollectionView view) return;
            if (!cb.IsKeyboardFocusWithin) return; // ignore programmatic/binding text changes

            string text = cb.Text?.Trim() ?? "";

            // If the text exactly matches the current selection, the user just picked an
            // item — show the full list again rather than filtering down to one entry.
            if (cb.SelectedItem is HostLevelOption sel &&
                string.Equals(sel.Name, text, StringComparison.OrdinalIgnoreCase))
            {
                view.Filter = null;
                return;
            }

            view.Filter = string.IsNullOrEmpty(text)
                ? null
                : o => o is HostLevelOption h &&
                       (h.Name?.IndexOf(text, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;

            cb.IsDropDownOpen = true;
        }

        private void NewLevelCombo_DropDownOpened(object sender, EventArgs e)
        {
            // Opening via the arrow should always show the full list.
            if (sender is ComboBox cb && cb.ItemsSource is ListCollectionView view
                && string.IsNullOrEmpty(cb.Text))
                view.Filter = null;
        }

        private void NewLevelCombo_DropDownClosed(object sender, EventArgs e)
        {
            if (sender is not ComboBox cb) return;

            if (cb.ItemsSource is ListCollectionView view) view.Filter = null;

            // Restore display text to the actual selection (or clear stale free text
            // that matched nothing) without re-triggering the filter.
            _suppressComboFilter = true;
            cb.Text = (cb.SelectedItem as HostLevelOption)?.Name ?? "";
            _suppressComboFilter = false;
        }

        // ─── Log panel ──────────────────────────────────────────────────────────

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = LogListBox.SelectedItems.Cast<LogEntry>().ToList();
            if (selected.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var entry in selected) sb.AppendLine(entry.ToString());
            Clipboard.SetText(sb.ToString());

            if (DataContext is MainViewModel vm) vm.ShowToast("Selected logs copied to clipboard");
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
