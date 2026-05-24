using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace QuickLook.Plugin.DevPowerTool.Helpers
{
    public sealed class SwatchInfo
    {
        public int   Line        { get; set; }
        public int   CharOffset  { get; set; }
        public int   TokenLength { get; set; }
        public Color Color       { get; set; }
    }

    public sealed class ColorSwatchRenderer : IBackgroundRenderer
    {
        private const double Size   = 10.0;
        private const double Radius =  2.0;
        private const double Gap    =  2.0;

        private readonly TextEditor       _editor;
        private readonly List<SwatchInfo> _swatches;
        private readonly Dictionary<Color, Pen> _pens = new Dictionary<Color, Pen>();

        public ColorSwatchRenderer(TextEditor editor, List<SwatchInfo> swatches)
        {
            _editor   = editor   ?? throw new ArgumentNullException("editor");
            _swatches = swatches ?? throw new ArgumentNullException("swatches");
        }

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext dc)
        {
            if (textView == null || dc == null || _swatches.Count == 0) return;

            textView.EnsureVisualLines();

            var doc = _editor.Document;
            if (doc == null || textView.VisualLines.Count == 0) return;

            foreach (var s in _swatches)
            {
                try { DrawOne(textView, dc, doc, s); }
                catch { }
            }
        }

        private void DrawOne(TextView tv, DrawingContext dc,
            ICSharpCode.AvalonEdit.Document.TextDocument doc, SwatchInfo s)
        {
            if (s.Line < 1 || s.Line > doc.LineCount) return;

            var docLine = doc.GetLineByNumber(s.Line);
            int absOff  = docLine.Offset + s.CharOffset;
            if (absOff < 0 || absOff >= doc.TextLength) return;

            var vl = tv.GetVisualLine(s.Line);
            if (vl == null) return;

            double x = vl.GetVisualColumn(s.CharOffset) * tv.WideSpaceWidth - Size - Gap - tv.HorizontalOffset;
            double y = vl.VisualTop - tv.VerticalOffset + (vl.Height - Size) / 2.0;

            if (x + Size < 0 || x > tv.ActualWidth)  return;
            if (y + Size < 0 || y > tv.ActualHeight)  return;

            var fill = System.Windows.Media.Color.FromRgb(s.Color.R, s.Color.G, s.Color.B);
            var brush = new SolidColorBrush(fill);
            brush.Freeze();

            if (!_pens.TryGetValue(fill, out var pen))
            {
                double f = 0.7;
                var bc = System.Windows.Media.Color.FromRgb(
                    (byte)(fill.R * f), (byte)(fill.G * f), (byte)(fill.B * f));
                pen = new Pen(new SolidColorBrush(bc), 0.75);
                pen.Freeze();
                _pens[fill] = pen;
            }

            var geo = new RectangleGeometry(new Rect(x, y, Size, Size), Radius, Radius);
            geo.Freeze();
            dc.DrawGeometry(brush, pen, geo);
        }
    }
}