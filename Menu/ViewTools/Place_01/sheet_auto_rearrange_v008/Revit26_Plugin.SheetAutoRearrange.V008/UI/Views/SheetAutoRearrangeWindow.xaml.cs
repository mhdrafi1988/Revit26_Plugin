using Revit26_Plugin.SheetAutoRearrange.V008.Core.Models;
using Revit26_Plugin.SheetAutoRearrange.V008.UI.ViewModels;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Revit26_Plugin.SheetAutoRearrange.V008.UI.Views
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
            Loaded += (_, _) =>
            {
                UpdatePreview();
                DrawRegionPreview();
            };

            HookGapSettingsEvents();
            HookTallWideSettingsEvents();
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

        /// <summary>
        /// V007 NEW: same reasoning as HookGapSettingsEvents — TallDetectionSettings
        /// and WideDetectionSettings are each their own ObservableObject, so edits
        /// to Multiplier/TolerancePercent/IsEnabled/OverflowGrouping need their own
        /// PropertyChanged subscription to refresh the live preview.
        /// </summary>
        private void HookTallWideSettingsEvents()
        {
            _viewModel.TallDetectionSettings.PropertyChanged += (_, _) => { _viewModel.RefreshSizeCategories(); UpdatePreview(); };
            _viewModel.WideDetectionSettings.PropertyChanged += (_, _) => { _viewModel.RefreshSizeCategories(); UpdatePreview(); };
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
                _viewModel.RefreshSizeCategories();
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

        // V007 NEW: Tall/Wide View Detection overflow-grouping radios.
        private void TallOverflowRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag
                && System.Enum.TryParse<ShelfOverflowGrouping>(tag, out var grouping))
            {
                _viewModel.TallDetectionSettings.OverflowGrouping = grouping;
            }
        }

        private void WideOverflowRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag
                && System.Enum.TryParse<ShelfOverflowGrouping>(tag, out var grouping))
            {
                _viewModel.WideDetectionSettings.OverflowGrouping = grouping;
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

                // V006 NEW: region changes (auto-detect, re-detect, manual apply)
                // must refresh BOTH the Placeable Area mini-preview and the main
                // Live Sheet Preview, since the main preview's scale math now
                // derives from Region.GetOverallBounds() instead of a flat
                // UsableAreaMin/Max pair.
                case nameof(SheetAutoRearrangeViewModel.Region):
                case nameof(SheetAutoRearrangeViewModel.IsManualFallback):
                case nameof(SheetAutoRearrangeViewModel.IsRegionLShape):
                case nameof(SheetAutoRearrangeViewModel.DetectionStatusText):
                    DrawRegionPreview();
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
            {
                _viewModel.RefreshSizeCategories();
                UpdatePreview();
            }
        }

        /// <summary>
        /// Draws a to-scale preview of where Run() would actually place each
        /// ticked view, by calling the ViewModel's PreviewPack() — which runs
        /// the SAME packing service Run() uses, purely in memory. This keeps
        /// the preview in exact parity with what Run() will do; it is not an
        /// approximation based on current positions.
        ///
        /// V006 CHANGE: scale/bounds math now derives from
        /// _viewModel.Region.GetOverallBounds() instead of the old flat
        /// UsableAreaMin/Max XYZ pair. Per confirmed scope, this pass does
        /// NOT draw the title-block notch / L-shape split in THIS canvas —
        /// that graphic lives only in the Placeable Area card's mini preview
        /// (DrawRegionPreview, below). If Region is null (e.g. multiple title
        /// blocks found), the canvas is cleared and left blank.
        /// </summary>
        private void UpdatePreview()
        {
            PreviewCanvas.Children.Clear();

            var region = _viewModel.Region;
            if (region == null)
                return;

            var placements = _viewModel.PreviewPack();
            if (placements.Count == 0)
                return;

            double canvasW = PreviewCanvas.ActualWidth > 0 ? PreviewCanvas.ActualWidth : 340;
            double canvasH = PreviewCanvas.ActualHeight > 0 ? PreviewCanvas.ActualHeight : 200;

            var overallBounds = region.GetOverallBounds();
            double usableWFeet = overallBounds.Width;
            double usableHFeet = overallBounds.Height;
            if (usableWFeet <= 0) usableWFeet = 1;
            if (usableHFeet <= 0) usableHFeet = 1;

            // Overflow views are placed below the sheet's bottom edge
            // (continuing the row layout downward) rather than skipped —
            // extend the vertical scale span to include them so they're
            // visible in the preview instead of being clipped off-canvas.
            double lowestYFeet = overallBounds.Min.Y;
            foreach (var p in placements)
            {
                double bottomFeet = p.NewCenter.Y - (p.Item.HeightMm / 304.8) / 2.0;
                if (bottomFeet < lowestYFeet)
                    lowestYFeet = bottomFeet;
            }
            double totalHFeet = (overallBounds.Max.Y - lowestYFeet);
            if (totalHFeet <= 0) totalHFeet = usableHFeet;

            double scale = System.Math.Min(canvasW / usableWFeet, canvasH / totalHFeet) * 0.94;

            // Draw the sheet's bottom-edge boundary line so overflow content
            // below it reads as clearly outside the sheet, not just "lower".
            double sheetBottomYPx = canvasH;
            if (lowestYFeet < overallBounds.Min.Y)
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

                double centerXFeet = placement.NewCenter.X - overallBounds.Min.X;
                double centerYFeet = placement.NewCenter.Y - overallBounds.Min.Y;

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

        /// <summary>
        /// V006 NEW: draws the small schematic in the Placeable Area card —
        /// sheet outline (dashed), Large rect (solid blue), Small rect
        /// (dashed blue, if L-shape), and a hatched block approximating the
        /// title block's footprint (derived as sheet-bounds minus the
        /// resolved region, for display purposes only — not used for any
        /// packing math). Blank if Region is null or Undetected with no
        /// manual override yet applied.
        /// </summary>
        private void DrawRegionPreview()
        {
            RegionPreviewCanvas.Children.Clear();

            var region = _viewModel.Region;
            if (region == null || region.Mode == TitleBlockDetectionMode.Undetected)
                return;

            double canvasW = RegionPreviewCanvas.ActualWidth > 0 ? RegionPreviewCanvas.ActualWidth : 340;
            double canvasH = RegionPreviewCanvas.ActualHeight > 0 ? RegionPreviewCanvas.ActualHeight : 100;

            var bounds = region.GetOverallBounds();
            double boundsW = bounds.Width <= 0 ? 1 : bounds.Width;
            double boundsH = bounds.Height <= 0 ? 1 : bounds.Height;

            double pad = 8;
            double scale = System.Math.Min((canvasW - pad * 2) / boundsW, (canvasH - pad * 2) / boundsH);

            // local helper: sheet-feet rect -> canvas pixel Rect (flip Y)
            Rect ToPx(RectFeet r)
            {
                double left = pad + (r.Min.X - bounds.Min.X) * scale;
                double right = pad + (r.Max.X - bounds.Min.X) * scale;
                double topPx = canvasH - pad - (r.Max.Y - bounds.Min.Y) * scale;
                double bottomPx = canvasH - pad - (r.Min.Y - bounds.Min.Y) * scale;
                return new Rect(left, topPx, System.Math.Max(right - left, 1), System.Math.Max(bottomPx - topPx, 1));
            }

            var largePx = ToPx(region.LargeRect);
            var largeRect = new Rectangle
            {
                Width = largePx.Width,
                Height = largePx.Height,
                Fill = new SolidColorBrush(Color.FromArgb(0x1A, 0x2D, 0x6C, 0xDF)),
                Stroke = new SolidColorBrush(Color.FromRgb(0x2D, 0x6C, 0xDF)),
                StrokeThickness = 1.4
            };
            Canvas.SetLeft(largeRect, largePx.Left);
            Canvas.SetTop(largeRect, largePx.Top);
            RegionPreviewCanvas.Children.Add(largeRect);

            if (region.SmallRect.HasValue)
            {
                var smallPx = ToPx(region.SmallRect.Value);
                var smallRect = new Rectangle
                {
                    Width = smallPx.Width,
                    Height = smallPx.Height,
                    Fill = new SolidColorBrush(Color.FromArgb(0x10, 0x2D, 0x6C, 0xDF)),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x5B, 0x8D, 0xEF)),
                    StrokeThickness = 1.2,
                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 3, 2 }
                };
                Canvas.SetLeft(smallRect, smallPx.Left);
                Canvas.SetTop(smallRect, smallPx.Top);
                RegionPreviewCanvas.Children.Add(smallRect);
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
