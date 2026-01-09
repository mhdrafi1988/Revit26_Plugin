using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace Revit26_Plugin.SectionManager_V07.Helpers
{
    /// <summary>
    /// Enables binding ListBox.SelectedItems to a ViewModel collection.
    /// Required for MVVM (WPF does not support this natively).
    /// </summary>
    public static class ListBoxSelectedItemsBehavior
    {
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItems",
                typeof(IList),
                typeof(ListBoxSelectedItemsBehavior),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedItemsChanged));

        public static void SetSelectedItems(DependencyObject element, IList value)
        {
            element.SetValue(SelectedItemsProperty, value);
        }

        public static IList GetSelectedItems(DependencyObject element)
        {
            return (IList)element.GetValue(SelectedItemsProperty);
        }

        private static void OnSelectedItemsChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is not ListBox listBox)
                return;

            listBox.SelectionChanged -= ListBox_SelectionChanged;

            if (e.NewValue != null)
            {
                listBox.SelectionChanged += ListBox_SelectionChanged;
            }
        }

        private static void ListBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (sender is not ListBox listBox)
                return;

            IList boundCollection =
                GetSelectedItems(listBox);

            if (boundCollection == null)
                return;

            // Remove unselected items
            foreach (var item in e.RemovedItems)
            {
                if (boundCollection.Contains(item))
                    boundCollection.Remove(item);
            }

            // Add newly selected items
            foreach (var item in e.AddedItems)
            {
                if (!boundCollection.Contains(item))
                    boundCollection.Add(item);
            }
        }
    }
}
