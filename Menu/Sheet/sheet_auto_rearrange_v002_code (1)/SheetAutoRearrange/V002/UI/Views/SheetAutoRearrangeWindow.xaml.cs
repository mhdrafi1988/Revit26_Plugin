using Revit26_Plugin.SheetAutoRearrange.V002.Core.Models;
using Revit26_Plugin.SheetAutoRearrange.V002.UI.ViewModels;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Revit26_Plugin.SheetAutoRearrange.V002.UI.Views
{
    public partial class SheetAutoRearrangeWindow : Window
    {
        private readonly SheetAutoRearrangeViewModel _viewModel;

        public SheetAutoRearrangeWindow(SheetAutoRearrangeViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Closed += (_, _) => _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            Loaded += (_, _) => UpdatePreview();

            HookGapSettingsEvents();
            HookViewItemEvents();
        }

        /// <summary>
        /// GapSettings and its per-group entries are their own ObservableObjects
        /// (not replaced by reference when edited), so the top-level ViewModel's
        /// PropertyChanged never fires for margin/gap edits — hook their
        /// PropertyChanged directly so the live preview stays in sync.
        /// </summary>
        private void HookGapSettingsEvents()
        {
            _viewModel.GapSettings.PropertyChanged += (_, _) => UpdatePreview();
            foreach (var group in _viewModel.GapSettings.Groups)
                group.PropertyChanged += (_, _) => UpdatePreview();
        }

        // ── View Types popover ──────────────────────────────────────────
        private void ViewTypesButton_Click(object sender, RoutedEventArgs e)
        {
            ViewTypesPopup.PlacementTarget = (UIElement)sender;
            ViewTypesPopup.Placement = PlacementMode.Bottom;
            ViewTypesPopup.IsOpen = !ViewTypesPopup.IsOpen;
        }

        // ── Grid checkbox: block row-select cascade, per DataGrid spec ──
        private void RowCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false; // allow the click to toggle the checkbox itself
            // Stops the click from also triggering DataGridRow selection:
            if (sender is DependencyObject d)
            {
                var row = FindParent<DataGridRow>(d);
                if (row != null)
                    row.IsSelected = false;
            }
        }

        // ── Algorithm / Overflow radio -> enum wiring ───────────────────
        private void AlgoRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag
                && System.Enum.TryParse<RearrangeAlgorithm>(tag, out var algo))
            {
                _viewModel.SelectedAlgorithm = algo;
                UpdatePreview();
            }
        }

        private void OverflowRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag
                && System.Enum.TryParse<OverflowHandlingMode>(tag, out var mode))
            {
                _viewModel.OverflowHandlingMode = mode;
            }
        }

        // ── Live preview redraw ──────────────────────────────────────────
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SheetAutoRearrangeViewModel.Views):
                    HookViewItemEvents();
                    UpdatePreview();
                    break;

                case nameof(SheetAutoRearrangeViewModel.RowToleranceMm):
                case nameof(SheetAutoRearrangeViewModel.RowAlignment):
                case nameof(SheetAutoRearrangeViewModel.BlockAlignH):
                case nameof(SheetAutoRearrangeViewModel.BlockAlignV):
                case nameof(SheetAutoRearrangeViewModel.SelectedAlgorithm):
                    UpdatePreview();
                    break;
            }
        }

        /// <summary>
        /// Each grid row's IsChecked toggle changes the ticked set PreviewPack()
        /// packs — hook every row's PropertyChanged so ticking/unticking
        /// refreshes the preview immediately. Unsubscribes from the previous
        /// Views collection's items first so replaced rows (after Load/Run)
        /// don't leak subscriptions on discarded ViewOnSheetItem instances.
        /// </summary>
        private System.Collections.Generic.List<ViewOnSheetItem> _hookedItems = new();

        private void HookViewItemEvents()
        {
            foreach (var old in _hookedItems)
                old.PropertyChanged -= ViewItem_PropertyChanged;

            _hookedItems = _viewModel.Views.ToList();
            foreach (var item in _hookedItems)
                item.PropertyChanged += ViewItem_PropertyChanged;
        }

        private void ViewItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewOnSheetItem.IsChecked))
                UpdatePreview();
        }

        /// <summary>
        /// Draws a to-scale preview of where Run() would actually place each
        /// ticked view, by calling the ViewModel's PreviewPack() — which runs
        /// the SAME packing service Run() uses, purely in memory. This keeps
        /// the preview in exact parity with what Run() will do; it is not an
        /// approximation based on current positions.
        /// </summary>
        private void UpdatePreview()
        {
            PreviewCanvas.Children.Clear();

            var placements = _viewModel.PreviewPack();
            if (placements.Count == 0)
                return;

            double canvasW = PreviewCanvas.ActualWidth > 0 ? PreviewCanvas.ActualWidth : 340;
            double canvasH = PreviewCanvas.ActualHeight > 0 ? PreviewCanvas.ActualHeight : 200;

            double usableWFeet = _viewModel.UsableAreaMax.X - _viewModel.UsableAreaMin.X;
            double usableHFeet = _viewModel.UsableAreaMax.Y - _viewModel.UsableAreaMin.Y;
            if (usableWFeet <= 0) usableWFeet = 1;
            if (usableHFeet <= 0) usableHFeet = 1;

            // Overflow views are now actually placed below the sheet's bottom
            // edge (continuing the row layout downward) rather than skipped —
            // extend the vertical scale span to include them so they're
            // visible in the preview instead of being clipped off-canvas.
            double lowestYFeet = _viewModel.UsableAreaMin.Y;
            foreach (var p in placements)
            {
                double bottomFeet = p.NewCenter.Y - (p.Item.HeightMm / 304.8) / 2.0;
                if (bottomFeet < lowestYFeet)
                    lowestYFeet = bottomFeet;
            }
            double totalHFeet = (_viewModel.UsableAreaMax.Y - lowestYFeet);
            if (totalHFeet <= 0) totalHFeet = usableHFeet;

            double scale = System.Math.Min(canvasW / usableWFeet, canvasH / totalHFeet) * 0.94;

            // Draw the sheet's bottom-edge boundary line so overflow content
            // below it reads as clearly outside the sheet, not just "lower".
            // The usable area's own bottom edge is at Y=0 in the "relative to
            // UsableAreaMin" space the rectangles below use, so it maps to
            // canvasH under the same flip-Y transform.
            double sheetBottomYPx = canvasH;
            if (lowestYFeet < _viewModel.UsableAreaMin.Y)
            {
                var boundaryLine = new System.Windows.Shapes.Line
                {
                    X1 = 0,
                    X2 = canvasW,
                    Y1 = sheetBottomYPx,
                    Y2 = sheetBottomYPx,
                    Stroke = new SolidColorBrush(Color.FromRgb(0xD9, 0x53, 0x4F)),
                    StrokeThickness = 1,
                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 3 }
                };
                PreviewCanvas.Children.Add(boundaryLine);
            }

            foreach (var placement in placements)
            {
                double wFeet = placement.Item.WidthMm / 304.8;
                double hFeet = placement.Item.HeightMm / 304.8;

                double centerXFeet = placement.NewCenter.X - _viewModel.UsableAreaMin.X;
                double centerYFeet = placement.NewCenter.Y - _viewModel.UsableAreaMin.Y;

                double wPx = wFeet * scale;
                double hPx = hFeet * scale;
                double centerXPx = centerXFeet * scale;
                double centerYPx = canvasH - (centerYFeet * scale); // flip Y (sheet-up vs canvas-down)

                double leftPx = centerXPx - wPx / 2.0;
                double topPx = centerYPx - hPx / 2.0;

                var rect = new Rectangle
                {
                    Width = System.Math.Max(wPx, 4),
                    Height = System.Math.Max(hPx, 4),
                    Fill = placement.Fits ? new SolidColorBrush(Color.FromRgb(0xEA, 0xF1, 0xF8))
                                          : new SolidColorBrush(Color.FromRgb(0xFD, 0xED, 0xEC)),
                    Stroke = placement.Fits ? new SolidColorBrush(Color.FromRgb(0x2D, 0x6C, 0xDF))
                                             : new SolidColorBrush(Color.FromRgb(0xD9, 0x53, 0x4F)),
                    StrokeThickness = 1.2,
                    RadiusX = 2,
                    RadiusY = 2
                };

                Canvas.SetLeft(rect, leftPx);
                Canvas.SetTop(rect, topPx);
                PreviewCanvas.Children.Add(rect);
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null && parent is not T)
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            return parent as T;
        }
    }
}
