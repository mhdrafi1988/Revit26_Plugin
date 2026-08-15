using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Revit26_Plugin.CalloutCOP.V018.ViewModels;

namespace Revit26_Plugin.CalloutCOP.V018.Views
{
    /// <summary>
    /// Attached behavior powering the searchable popover on the shared
    /// DraftingViewComboBox template (V018). Each of the 6 ComboBox
    /// instances (3 bulk-fill + 3 per-row grid) shares the same
    /// DraftingViews source collection, so a single CollectionViewSource on
    /// that shared collection would fight itself across instances - this
    /// creates one independent, per-ComboBox CollectionView instead.
    ///
    /// Wired via the ComboBox's own Loaded event in the ControlTemplate
    /// (see CalloutCOPWindow.xaml) rather than markup-only binding, because
    /// WPF has no built-in "live filtered popup + counter" combo control.
    /// </summary>
    public static class DraftingViewSearchBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled", typeof(bool), typeof(DraftingViewSearchBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ComboBox combo || e.NewValue is not true)
                return;

            // Popup content is a logically disconnected visual tree - FindName
            // can't see PART_SearchBox/PART_ResultCount until the Popup has
            // actually opened at least once, so attach lazily on first open
            // rather than on the ComboBox's own Loaded event.
            combo.DropDownOpened += FirstOpen_Attach;
        }

        private static void FirstOpen_Attach(object sender, EventArgs e)
        {
            var combo = (ComboBox)sender;
            combo.DropDownOpened -= FirstOpen_Attach;
            Attach(combo);
        }

        private static void Attach(ComboBox combo)
        {
            if (combo.Template?.FindName("PART_SearchBox", combo) is not TextBox searchBox
                || combo.Template.FindName("PART_ResultCount", combo) is not TextBlock countText)
                return;

            var view = new ListCollectionView(((System.Collections.IEnumerable)combo.ItemsSource).Cast<DraftingViewItemViewModel>().ToList())
            {
                Filter = o => FilterItem(o, searchBox.Text)
            };
            combo.ItemsSource = view;

            void ResetAndFocus()
            {
                searchBox.Text = string.Empty;
                view.Filter = o => FilterItem(o, string.Empty);
                UpdateCount();
                searchBox.Focus();
            }

            void UpdateCount() => countText.Text = $"Showing {view.Count}/{view.SourceCollection.Cast<object>().Count()}";

            searchBox.TextChanged += (_, _) =>
            {
                view.Filter = o => FilterItem(o, searchBox.Text);
                UpdateCount();
            };

            // Subsequent opens (the first open already brought us here).
            combo.DropDownOpened += (_, _) => ResetAndFocus();

            // Handle the open that got us here in the first place.
            ResetAndFocus();
        }

        // V018: supports two search modes.
        //  - "12/A-201"  -> structured: left of '/' must match a Detail #
        //                   token, right of '/' must match a Sheet # token
        //                   (independently - not required to be the same
        //                   placement pair). Either side can be blank to
        //                   mean "don't care" (e.g. "/A-201" or "12/").
        //  - anything else -> free text OR-matched across Name, Sheet #s,
        //                     and Detail #s, same as before.
        private static bool FilterItem(object o, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            if (o is not DraftingViewItemViewModel item)
                return false;

            var slashIndex = search.IndexOf('/');
            if (slashIndex >= 0)
            {
                var detailPart = search[..slashIndex].Trim();
                var sheetPart = search[(slashIndex + 1)..].Trim();

                var detailOk = string.IsNullOrEmpty(detailPart) || MatchesAnyToken(item.DetailNumbers, detailPart);
                var sheetOk = string.IsNullOrEmpty(sheetPart) || MatchesAnyToken(item.SheetNumbers, sheetPart);

                return detailOk && sheetOk;
            }

            return item.Name?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || item.SheetNumbers?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || item.DetailNumbers?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// True if any comma-separated token in the joined field (e.g.
        /// "12, 15" or "A-201, A-203") contains <paramref name="query"/>.
        /// Token-based rather than a raw substring check on the whole joined
        /// string, so searching "1" for a detail-number query doesn't
        /// false-positive-match inside an unrelated "12".
        /// </summary>
        private static bool MatchesAnyToken(string joinedField, string query)
        {
            if (string.IsNullOrWhiteSpace(joinedField))
                return false;

            return joinedField.Split(',')
                .Select(t => t.Trim())
                .Any(t => t.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

    }
}
