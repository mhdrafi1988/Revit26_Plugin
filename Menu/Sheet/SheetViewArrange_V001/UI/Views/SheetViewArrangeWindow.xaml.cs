using Revit26_Plugin.SheetViewArrange.V001.Core.Layout;
using Revit26_Plugin.SheetViewArrange.V001.UI.ViewModels;
using Revit26_Plugin.Shared.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Revit26_Plugin.SheetViewArrange.V001.UI.Views
{
    /// <summary>
    /// Sheet View Arrange window. Draws the to-scale preview of <see cref="SheetViewArrangeViewModel.Plan"/>
    /// on a canvas; everything else is bound.
    /// </summary>
    public partial class SheetViewArrangeWindow : Window
    {
        private static readonly Brush FrameStroke = Frozen(Color.FromRgb(0x8F, 0xA3, 0xB8));
        private static readonly Brush AreaStroke = Frozen(Color.FromRgb(0x5B, 0x71, 0x85));
        private static readonly Brush FitFill = Frozen(Color.FromRgb(0xEA, 0xF1, 0xF8));
        private static readonly Brush FitStroke = Frozen(Color.FromRgb(0x2D, 0x6C, 0xDF));
        private static readonly Brush NoFitFill = Frozen(Color.FromRgb(0xFD, 0xED, 0xEC));
        private static readonly Brush NoFitStroke = Frozen(Color.FromRgb(0xD9, 0x53, 0x4F));
        private static readonly Brush SkipStroke = Frozen(Color.FromRgb(0x99, 0x99, 0x99));
        private static readonly Brush LabelBrush = Frozen(Color.FromRgb(0x1A, 0x2B, 0x3C));
        private static readonly Brush SkipFill = CreateHatch();

        private readonly SheetViewArrangeViewModel _viewModel;

        /// <summary>Creates the window for <paramref name="viewModel"/>.</summary>
        public SheetViewArrangeWindow(SheetViewArrangeViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            Title = ToolCatalog.SheetViewArrange.Title;

            _viewModel.PlanChanged += OnPlanChanged;
            PreviewKeyDown += OnPreviewKeyDown;
            Closing += (_, _) => _viewModel.SaveSettings();
            Closed += (_, _) =>
            {
                _viewModel.PlanChanged -= OnPlanChanged;
                _viewModel.Dispose();
            };
            Loaded += (_, _) => DrawPreview();
        }

        /// <summary>Esc closes; Enter in a number box commits it (boxes otherwise update on focus loss).</summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
            else if (e.Key == Key.Enter && Keyboard.FocusedElement is TextBox box)
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        private void OnPlanChanged(object sender, EventArgs e) => DrawPreview();

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawPreview();

        /// <summary>
        /// Draws title block frame, usable area, planned positions (blue, red when they don't fit)
        /// and skipped views at their current position (grey hatch). Sheet Y points up, canvas Y down.
        /// </summary>
        private void DrawPreview()
        {
            PreviewCanvas.Children.Clear();
            var plan = _viewModel.Plan;
            double canvasW = PreviewCanvas.ActualWidth, canvasH = PreviewCanvas.ActualHeight;
            if (plan == null || canvasW < 20 || canvasH < 20)
                return;

            // World extent: frame (or area) plus everything drawn, so overflow stays visible.
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            void Include(LayoutRect r)
            {
                minX = Math.Min(minX, r.MinX); minY = Math.Min(minY, r.MinY);
                maxX = Math.Max(maxX, r.MaxX); maxY = Math.Max(maxY, r.MaxY);
            }

            if (plan.Sheet.TitleBlockFrame is LayoutRect frame) Include(frame);
            if (plan.Area is LayoutRect area) Include(area);
            foreach (var m in plan.Moves) Include(m.Target);
            foreach (var s in plan.Skipped) Include(s.Footprint);
            if (plan.Moves.Count == 0)
                foreach (var v in plan.Sheet.Viewports) Include(v.Footprint);
            if (minX >= maxX || minY >= maxY)
                return;

            const double pad = 8;
            double scale = Math.Min((canvasW - 2 * pad) / (maxX - minX), (canvasH - 2 * pad) / (maxY - minY));
            double offX = (canvasW - (maxX - minX) * scale) / 2;
            double offY = (canvasH - (maxY - minY) * scale) / 2;

            Rect ToCanvas(LayoutRect r) => new(
                offX + (r.MinX - minX) * scale,
                offY + (maxY - r.MaxY) * scale,
                Math.Max(1, r.Width * scale),
                Math.Max(1, r.Height * scale));

            if (plan.Sheet.TitleBlockFrame is LayoutRect f)
                AddRect(ToCanvas(f), Brushes.White, FrameStroke, 1, null);
            if (plan.Area is LayoutRect a)
                AddRect(ToCanvas(a), null, AreaStroke, 1, new DoubleCollection { 4, 3 });

            foreach (var s in plan.Skipped)
                AddRect(ToCanvas(s.Footprint), SkipFill, SkipStroke, 1, null, s.DetailNumber);

            if (plan.Moves.Count > 0)
            {
                foreach (var m in plan.Moves)
                    AddRect(ToCanvas(m.Target), m.Fits ? FitFill : NoFitFill, m.Fits ? FitStroke : NoFitStroke, 1.2, null, m.Viewport.DetailNumber);
            }
            else
            {
                // No usable area: show where the views are now.
                foreach (var v in plan.Sheet.Viewports)
                    AddRect(ToCanvas(v.Footprint), NoFitFill, NoFitStroke, 1, null, v.DetailNumber);
            }
        }

        private void AddRect(Rect r, Brush fill, Brush stroke, double thickness, DoubleCollection dash, string label = null)
        {
            var rect = new Rectangle
            {
                Width = r.Width,
                Height = r.Height,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeDashArray = dash
            };
            Canvas.SetLeft(rect, r.X);
            Canvas.SetTop(rect, r.Y);
            PreviewCanvas.Children.Add(rect);

            if (string.IsNullOrWhiteSpace(label) || r.Width < 10 || r.Height < 10)
                return;

            var text = new TextBlock
            {
                Text = label,
                FontSize = Math.Max(8, Math.Min(13, r.Height / 3)),
                FontWeight = FontWeights.SemiBold,
                Foreground = LabelBrush,
                Width = r.Width,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Canvas.SetLeft(text, r.X);
            Canvas.SetTop(text, r.Y + r.Height / 2 - text.FontSize * 0.7);
            PreviewCanvas.Children.Add(text);
        }

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private static Brush CreateHatch()
        {
            var brush = new DrawingBrush
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 6, 6),
                ViewportUnits = BrushMappingMode.Absolute,
                Drawing = new GeometryDrawing(
                    null,
                    new Pen(new SolidColorBrush(Color.FromRgb(0xC3, 0xCF, 0xDB)), 1),
                    new LineGeometry(new Point(0, 6), new Point(6, 0)))
            };
            brush.Freeze();
            return brush;
        }

        private void CopySelectedLogs_Click(object sender, RoutedEventArgs e)
            => LogClipboardService.CopySelected(LogListBox.SelectedItems);

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
