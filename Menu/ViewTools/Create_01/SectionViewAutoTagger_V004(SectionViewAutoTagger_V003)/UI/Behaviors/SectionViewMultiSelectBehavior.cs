using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Revit26_Plugin.SectionViewAutoTagger.V004
{
    /// <summary>
    /// Attached behavior powering the searchable multi-select popover on
    /// "Section Views on Sheet". Adapted from the single-select
    /// DraftingViewSearchBehavior pattern (CalloutCOP V018) — the difference
    /// here is the list items are themselves checkable SectionViewOption
    /// rows (IsSelected is the VM-bound checkbox state), so there is no
    /// "selection closes the popover" behavior; the popover stays open
    /// across multiple checks and closes only via the Done button or
    /// clicking outside.
    ///
    /// Wired via the ToggleButton/Popup's own Loaded event in the
    /// ControlTemplate (see SectionViewAutoTaggerWindow.xaml), because WPF
    /// Popup content is not part of the visual tree until the popup opens
    /// at least once — Template.FindName is unreliable at Loaded, so
    /// filtering is wired on the Popup's Opened event instead.
    /// </summary>
    public static class SectionViewMultiSelectBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled", typeof(bool), typeof(SectionViewMultiSelectBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        // Internal state per Popup instance — search box text drives a
        // CollectionView filter over the bound SectionViewsOnSheet source.
        private static readonly DependencyProperty CollectionViewProperty =
            DependencyProperty.RegisterAttached(
                "CollectionView", typeof(ICollectionView), typeof(SectionViewMultiSelectBehavior));

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Popup popup || e.NewValue is not true)
                return;

            popup.Opened += (_, _) => Attach(popup);
        }

        private static void Attach(Popup popup)
        {
            // Idempotent guard — Opened fires every time the popup shows;
            // only wire the search box once per popup instance.
            if (popup.GetValue(CollectionViewProperty) != null)
            {
                RefreshCount(popup);
                return;
            }

            if (popup.Child is not FrameworkElement root) return;

            var searchBox = root.FindName("SearchBox") as TextBox;
            var listBox = root.FindName("ResultsListBox") as ItemsControl;
            var countText = root.FindName("CountText") as TextBlock;

            if (listBox?.ItemsSource == null) return;

            var view = CollectionViewSource.GetDefaultView(listBox.ItemsSource);
            popup.SetValue(CollectionViewProperty, view);

            if (searchBox != null)
            {
                searchBox.TextChanged += (_, _) =>
                {
                    string term = searchBox.Text?.Trim() ?? "";
                    view.Filter = term.Length == 0
                        ? null
                        : item => item is SectionViewOption sv
                                  && sv.ViewName?.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    RefreshCount(popup, view, countText);
                };
            }

            RefreshCount(popup, view, countText);
        }

        private static void RefreshCount(Popup popup)
        {
            var view = popup.GetValue(CollectionViewProperty) as ICollectionView;
            var root = popup.Child as FrameworkElement;
            var countText = root?.FindName("CountText") as TextBlock;
            RefreshCount(popup, view, countText);
        }

        private static void RefreshCount(Popup popup, ICollectionView view, TextBlock countText)
        {
            if (view == null || countText == null) return;

            int total = view.SourceCollection?.Cast<object>().Count() ?? 0;
            int shown = view.Cast<object>().Count();
            countText.Text = $"Showing {shown} / {total}";
        }
    }
}
