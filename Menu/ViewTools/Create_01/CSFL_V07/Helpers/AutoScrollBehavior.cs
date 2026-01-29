using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Revit26_Plugin.CSFL_V07.Helpers
{
    public static class AutoScrollBehavior
    {
        public static readonly DependencyProperty EnableProperty =
            DependencyProperty.RegisterAttached(
                "Enable",
                typeof(bool),
                typeof(AutoScrollBehavior),
                new PropertyMetadata(false, OnChanged));

        public static void SetEnable(DependencyObject d, bool v)
            => d.SetValue(EnableProperty, v);

        private static void OnChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ItemsControl ic) return;

            if (ic.Items is INotifyCollectionChanged col)
            {
                col.CollectionChanged += (_, __) =>
                {
                    if (VisualTreeHelper.GetChild(ic, 0) is Border b &&
                        VisualTreeHelper.GetChild(b, 0) is ScrollViewer sv)
                        sv.ScrollToEnd();
                };
            }
        }
    }
}
