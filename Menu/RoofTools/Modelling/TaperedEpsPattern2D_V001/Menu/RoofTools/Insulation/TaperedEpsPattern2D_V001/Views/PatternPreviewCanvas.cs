using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Revit26_Plugin.TaperedEpsPattern2D.V001.Core.Pattern;

namespace Revit26_Plugin.TaperedEpsPattern2D.V001.Views
{
    /// <summary>
    /// Live 2D preview. Draws exactly the segments of <see cref="Result"/> (what Run will create),
    /// one colour per layer, and reports the board under the mouse through <see cref="HoverText"/>.
    /// Colours come from SharedStyles tokens (see README) with system-colour fallbacks.
    /// </summary>
    public sealed class PatternPreviewCanvas : FrameworkElement
    {
        public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(
            nameof(Result), typeof(Pattern2DResult), typeof(PatternPreviewCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((PatternPreviewCanvas)d)._hovered = null));

        public static readonly DependencyProperty HoverTextProperty = DependencyProperty.Register(
            nameof(HoverText), typeof(string), typeof(PatternPreviewCanvas),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        private const double Padding = 16;
        private const int MaxLabelsDrawn = 400;

        private BoardPiece? _hovered;
        private double _scale = 1;
        private double _offsetX;
        private double _offsetY;
        private Vec2 _min;

        public Pattern2DResult? Result
        {
            get => (Pattern2DResult?)GetValue(ResultProperty);
            set => SetValue(ResultProperty, value);
        }

        public string HoverText
        {
            get => (string)GetValue(HoverTextProperty);
            set => SetValue(HoverTextProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize)); // hit-testable surface

            Pattern2DResult? r = Result;
            if (r == null || !r.IsValid || r.Outline.IsEmpty)
            {
                return;
            }

            UpdateTransform(r.Outline);

            if (_hovered != null)
            {
                dc.DrawGeometry(ResolveBrush("EpsPreview.HoverBrush", SystemColors.HighlightBrush, 0.25), null, ToGeometry(_hovered.Polygon));
            }

            dc.DrawGeometry(null, MakePen("EpsPreview.OutlineBrush", SystemColors.ControlTextBrush, 1.5), ToGeometry(r.Outline));

            DrawLayer(dc, r, PatternLayer.Contour, MakePen("EpsPreview.ContourBrush", SystemColors.HotTrackBrush, 1.0, dashed: true));
            DrawLayer(dc, r, PatternLayer.Grid, MakePen("EpsPreview.GridBrush", SystemColors.GrayTextBrush, 0.75));
            DrawLayer(dc, r, PatternLayer.HipRidge, MakePen("EpsPreview.HipRidgeBrush", SystemColors.HighlightBrush, 2.0));

            if (r.Labels.Count > 0 && r.Labels.Count <= MaxLabelsDrawn)
            {
                Brush textBrush = ResolveBrush("EpsPreview.LabelBrush", SystemColors.ControlTextBrush, 1);
                double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                foreach (PatternLabel label in r.Labels)
                {
                    var text = new FormattedText(label.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 9, textBrush, dpi);
                    Point p = ToScreen(label.Position);
                    dc.DrawText(text, new Point(p.X - text.Width / 2, p.Y - text.Height / 2));
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Pattern2DResult? r = Result;
            if (r == null || !r.IsValid || _scale <= 0)
            {
                return;
            }

            Vec2 world = ToWorld(e.GetPosition(this));
            BoardPiece? hit = r.Boards.FirstOrDefault(b => b.Polygon.ContainsConvex(world, 0));
            if (!ReferenceEquals(hit, _hovered))
            {
                _hovered = hit;
                HoverText = hit == null
                    ? string.Empty
                    : string.Format(CultureInfo.CurrentCulture, "{0}  ·  code {1}  ·  {2:0}–{3:0} mm  ·  {4}",
                        hit.Id, hit.Code, hit.MinThicknessMm, hit.MaxThicknessMm, hit.IsFull ? "full board" : "cut board");
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hovered != null)
            {
                _hovered = null;
                HoverText = string.Empty;
                InvalidateVisual();
            }
        }

        private void DrawLayer(DrawingContext dc, Pattern2DResult r, PatternLayer layer, Pen pen)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                foreach (PatternSegment s in r.SegmentsOf(layer))
                {
                    ctx.BeginFigure(ToScreen(s.Segment.Start), false, false);
                    ctx.LineTo(ToScreen(s.Segment.End), true, false);
                }
            }

            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }

        private void UpdateTransform(Polygon2D outline)
        {
            (Vec2 min, Vec2 max) = outline.BoundingBox();
            double w = Math.Max(max.X - min.X, 1e-6);
            double h = Math.Max(max.Y - min.Y, 1e-6);
            double availW = Math.Max(ActualWidth - 2 * Padding, 1);
            double availH = Math.Max(ActualHeight - 2 * Padding, 1);
            _scale = Math.Min(availW / w, availH / h);
            _min = min;
            _offsetX = Padding + (availW - w * _scale) / 2;
            _offsetY = Padding + (availH - h * _scale) / 2 + h * _scale; // Y up
        }

        private Point ToScreen(Vec2 p) => new Point(_offsetX + (p.X - _min.X) * _scale, _offsetY - (p.Y - _min.Y) * _scale);

        private Vec2 ToWorld(Point p) => new Vec2(_min.X + (p.X - _offsetX) / _scale, _min.Y + (_offsetY - p.Y) / _scale);

        private Geometry ToGeometry(Polygon2D polygon)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(ToScreen(polygon.Vertices[0]), true, true);
                foreach (Vec2 v in polygon.Vertices.Skip(1))
                {
                    ctx.LineTo(ToScreen(v), true, false);
                }
            }

            geometry.Freeze();
            return geometry;
        }

        private Brush ResolveBrush(string key, Brush fallback, double opacity)
        {
            Brush brush = TryFindResource(key) as Brush ?? fallback;
            if (opacity >= 1)
            {
                return brush;
            }

            Brush clone = brush.CloneCurrentValue();
            clone.Opacity = opacity;
            clone.Freeze();
            return clone;
        }

        private Pen MakePen(string key, Brush fallback, double thickness, bool dashed = false)
        {
            var pen = new Pen(ResolveBrush(key, fallback, 1), thickness);
            if (dashed)
            {
                pen.DashStyle = DashStyles.Dash;
            }

            pen.Freeze();
            return pen;
        }
    }
}
