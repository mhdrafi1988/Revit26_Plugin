using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Revit26_Plugin.Shared.Controls
{
    /// <summary>
    /// Attached properties for content hosted in a <see cref="ToolWindowShell"/> body
    /// (CLAUDE.md v1.1). Apply both to every DataGrid and log ListBox:
    /// <code>
    /// xmlns:shell="clr-namespace:Revit26_Plugin.Shared.Controls"
    /// &lt;DataGrid shell:ShellBehaviors.MaxHeightRatio="0.45"
    ///           shell:ShellBehaviors.ForwardMouseWheel="True" .../&gt;
    /// </code>
    /// </summary>
    public static class ShellBehaviors
    {
        // ─── MaxHeightRatio ──────────────────────────────────────────
        // Caps the element's MaxHeight at (owning Window.ActualHeight × ratio)
        // and tracks window resizes. Inside the shell's ScrollViewer a list
        // otherwise grows to full height and loses its own scrolling.

        public static readonly DependencyProperty MaxHeightRatioProperty =
            DependencyProperty.RegisterAttached("MaxHeightRatio", typeof(double), typeof(ShellBehaviors),
                new PropertyMetadata(0d, OnMaxHeightRatioChanged));

        public static double GetMaxHeightRatio(DependencyObject d) => (double)d.GetValue(MaxHeightRatioProperty);
        public static void SetMaxHeightRatio(DependencyObject d, double value) => d.SetValue(MaxHeightRatioProperty, value);

        // Window + SizeChanged handler currently tracked for an element.
        private static readonly DependencyProperty TrackedWindowProperty =
            DependencyProperty.RegisterAttached("TrackedWindow", typeof(Window), typeof(ShellBehaviors));
        private static readonly DependencyProperty SizeHandlerProperty =
            DependencyProperty.RegisterAttached("SizeHandler", typeof(SizeChangedEventHandler), typeof(ShellBehaviors));

        private static void OnMaxHeightRatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FrameworkElement fe) return;

            fe.Loaded -= MaxHeight_Loaded;
            fe.Unloaded -= MaxHeight_Unloaded;
            Detach(fe);

            if ((double)e.NewValue > 0)
            {
                fe.Loaded += MaxHeight_Loaded;
                fe.Unloaded += MaxHeight_Unloaded;
                if (fe.IsLoaded) Attach(fe);
            }
        }

        private static void MaxHeight_Loaded(object sender, RoutedEventArgs e) => Attach((FrameworkElement)sender);
        private static void MaxHeight_Unloaded(object sender, RoutedEventArgs e) => Detach((FrameworkElement)sender);

        private static void Attach(FrameworkElement fe)
        {
            Detach(fe);
            var window = Window.GetWindow(fe);
            if (window == null) return;

            SizeChangedEventHandler handler = (_, __) => Apply(fe, window);
            window.SizeChanged += handler;
            fe.SetValue(TrackedWindowProperty, window);
            fe.SetValue(SizeHandlerProperty, handler);
            Apply(fe, window);
        }

        private static void Detach(FrameworkElement fe)
        {
            if (fe.GetValue(TrackedWindowProperty) is Window window &&
                fe.GetValue(SizeHandlerProperty) is SizeChangedEventHandler handler)
            {
                window.SizeChanged -= handler;
            }
            fe.ClearValue(TrackedWindowProperty);
            fe.ClearValue(SizeHandlerProperty);
        }

        private static void Apply(FrameworkElement fe, Window window)
        {
            double ratio = GetMaxHeightRatio(fe);
            if (ratio <= 0 || window.ActualHeight <= 0) return;
            fe.MaxHeight = Math.Max(fe.MinHeight, window.ActualHeight * ratio);
        }

        // ─── ForwardMouseWheel ───────────────────────────────────────
        // When the element's own ScrollViewer is already at the top/bottom
        // (or it has nothing to scroll), the wheel is re-raised on the parent
        // so the shell body keeps scrolling instead of the wheel dying here.

        public static readonly DependencyProperty ForwardMouseWheelProperty =
            DependencyProperty.RegisterAttached("ForwardMouseWheel", typeof(bool), typeof(ShellBehaviors),
                new PropertyMetadata(false, OnForwardMouseWheelChanged));

        public static bool GetForwardMouseWheel(DependencyObject d) => (bool)d.GetValue(ForwardMouseWheelProperty);
        public static void SetForwardMouseWheel(DependencyObject d, bool value) => d.SetValue(ForwardMouseWheelProperty, value);

        private static void OnForwardMouseWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element) return;
            element.PreviewMouseWheel -= Element_PreviewMouseWheel;
            if ((bool)e.NewValue) element.PreviewMouseWheel += Element_PreviewMouseWheel;
        }

        private static void Element_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || sender is not DependencyObject element) return;

            var inner = FindDescendant<ScrollViewer>(element);
            bool atTop = inner == null || inner.VerticalOffset <= 0;
            bool atBottom = inner == null || inner.VerticalOffset >= inner.ScrollableHeight;
            bool forward = e.Delta > 0 ? atTop : atBottom;
            if (!forward) return;

            if (VisualTreeHelper.GetParent(element) is not UIElement parent) return;

            e.Handled = true;
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = element
            });
        }

        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) return match;
                var nested = FindDescendant<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
