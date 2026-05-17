// ============================================================
// QuickLook.Plugin.DevPowerTool — Helpers/ColorSwatchRenderer.cs
//
// Draws a small colour box immediately before each colour token
// (before the #, rgba(, hsl( etc).
//
// Uses KnownLayer.Background so it renders under the text.
// Gets the rect of the token itself from AvalonEdit, then draws
// the box just to the left of that rect.
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
        private const double Gap       = 3; // gap between box right edge and token text

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

            // Get the total width of all left margins (line numbers etc.)
            // so we can correctly position the swatch to the left of the token.
            double marginWidth = textView.ActualWidth - textView.DocumentWidth;
            if (marginWidth < 0) marginWidth = 0;

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

                // GetRectsForSegment returns rects in visual coordinates
                var rects = BackgroundGeometryBuilder.GetRectsForSegment(textView, seg).ToList();
                if (rects.Count == 0) continue;

                var rect = rects[0];

                // The box sits just to the left of the token start
                double boxX = rect.Left - BoxSize - Gap;
                double boxY = rect.Top + (rect.Height - BoxSize) / 2.0;

                // If box would go into the line number margin or off screen, skip
                if (boxX < marginWidth) continue;

                var brush = new SolidColorBrush(s.Color);
                brush.Freeze();

                bool isDark = (0.299 * s.Color.R + 0.587 * s.Color.G + 0.114 * s.Color.B) < 128;
                var borderBrush = new SolidColorBrush(isDark
                    ? Color.FromArgb(100, 255, 255, 255)
                    : Color.FromArgb(100, 0,   0,   0));
                borderBrush.Freeze();

                var pen = new Pen(borderBrush, 0.8);
                pen.Freeze();

                ctx.DrawRoundedRectangle(
                    brush, pen,
                    new Rect(boxX, boxY, BoxSize, BoxSize),
                    BoxRadius, BoxRadius);
            }
        }
    }
}