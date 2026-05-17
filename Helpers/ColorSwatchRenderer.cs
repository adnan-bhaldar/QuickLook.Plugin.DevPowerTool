// ============================================================
// QuickLook.Plugin.DevPowerTool — Helpers/ColorSwatchRenderer.cs
//
// Draws a small colour box immediately before each colour token
// (#, rgb(, rgba(, hsl(, hsla( etc).
// Uses KnownLayer.Background and GetRectsForSegment to find the
// exact pixel position of each token in the rendered text.
// ============================================================

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace QuickLook.Plugin.DevPowerTool
{
    public sealed class SwatchInfo
    {
        public int   Line;
        public int   CharOffset;
        public int   TokenLength;
        public Color Color;
    }

    public sealed class ColorSwatchRenderer : IBackgroundRenderer
    {
        private const double BoxSize   = 10;
        private const double BoxRadius = 2;
        private const double Gap       = 3;

        private readonly TextEditor       _editor;
        private readonly List<SwatchInfo> _swatches;

        public ColorSwatchRenderer(TextEditor editor, List<SwatchInfo> swatches)
        {
            _editor   = editor;
            _swatches = swatches;
        }

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext ctx)
        {
            if (_swatches == null || _swatches.Count == 0) return;
            textView.EnsureVisualLines();

            // Sum the width of all left-side margins (line numbers etc.)
            double marginWidth = textView.LeftMargins
                .OfType<FrameworkElement>()
                .Sum(m => m.ActualWidth);

            foreach (var s in _swatches)
            {
                if (s.Line < 1 || s.Line > _editor.Document.LineCount) continue;

                var docLine  = _editor.Document.GetLineByNumber(s.Line);
                int tokStart = docLine.Offset + s.CharOffset;
                int tokLen   = s.TokenLength > 0 ? s.TokenLength : 1;
                int tokEnd   = tokStart + tokLen;

                if (tokStart >= docLine.EndOffset) continue;
                if (tokEnd   >  docLine.EndOffset) tokEnd = docLine.EndOffset;

                var seg = new ICSharpCode.AvalonEdit.Document.TextSegment
                {
                    StartOffset = tokStart,
                    EndOffset   = tokEnd
                };

                var rects = BackgroundGeometryBuilder.GetRectsForSegment(textView, seg).ToList();
                if (rects.Count == 0) continue;

                var rect = rects[0];

                double boxX = rect.Left - BoxSize - Gap;
                double boxY = rect.Top + (rect.Height - BoxSize) / 2.0;

                // Skip if box falls inside the line number margin
                if (boxX < marginWidth) continue;

                var fillBrush = new SolidColorBrush(s.Color);
                fillBrush.Freeze();

                bool isDark = (0.299 * s.Color.R + 0.587 * s.Color.G + 0.114 * s.Color.B) < 128;
                var borderBrush = new SolidColorBrush(isDark
                    ? Color.FromArgb(120, 255, 255, 255)
                    : Color.FromArgb(120, 0, 0, 0));
                borderBrush.Freeze();

                var pen = new Pen(borderBrush, 0.8);
                pen.Freeze();

                ctx.DrawRoundedRectangle(
                    fillBrush, pen,
                    new Rect(boxX, boxY, BoxSize, BoxSize),
                    BoxRadius, BoxRadius);
            }
        }
    }
}
