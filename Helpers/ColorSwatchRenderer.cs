// ============================================================
// QuickLook.Plugin.DevPowerTool — Helpers/ColorSwatchRenderer.cs
//
// Draws colour swatches using TWO approaches:
//
// 1. A small filled square BEHIND the colour token text
//    (drawn on the Background layer — always visible)
// 2. A thin coloured underline below the token
//
// This guarantees swatches are visible regardless of the
// line number margin width or scroll position.
// ============================================================

using System.Collections.Generic;
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
        private const double SwatchH  = 3;  // underline height
        private const double BoxSize  = 10; // small box size

        private readonly TextEditor       _editor;
        private readonly List<SwatchInfo> _swatches;

        public ColorSwatchRenderer(TextEditor editor, List<SwatchInfo> swatches)
        {
            _editor   = editor;
            _swatches = swatches;
        }

        // Use Background layer so we draw UNDER the text
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext ctx)
        {
            if (_swatches == null || _swatches.Count == 0) return;
            textView.EnsureVisualLines();

            foreach (var s in _swatches)
            {
                if (s.Line < 1 || s.Line > _editor.Document.LineCount) continue;

                var docLine   = _editor.Document.GetLineByNumber(s.Line);
                int tokStart  = docLine.Offset + s.CharOffset;
                int tokLength = s.TokenLength > 0 ? s.TokenLength : 1;
                int tokEnd    = tokStart + tokLength;

                if (tokStart >= docLine.EndOffset) continue;
                if (tokEnd   >  docLine.EndOffset) tokEnd = docLine.EndOffset;

                var seg = new ICSharpCode.AvalonEdit.Document.TextSegment
                {
                    StartOffset = tokStart,
                    EndOffset   = tokEnd
                };

                var brush = new SolidColorBrush(s.Color);
                brush.Freeze();

                bool isDark       = (0.299 * s.Color.R + 0.587 * s.Color.G + 0.114 * s.Color.B) < 128;
                var  borderColor  = isDark
                    ? Color.FromArgb(120, 255, 255, 255)
                    : Color.FromArgb(120, 0,   0,   0);
                var  borderBrush  = new SolidColorBrush(borderColor);
                borderBrush.Freeze();
                var  pen = new Pen(borderBrush, 0.8);
                pen.Freeze();

                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, seg))
                {
                    // Draw a small coloured box at the start of the token
                    double boxX = rect.Left;
                    double boxY = rect.Top + (rect.Height - BoxSize) / 2.0;

                    ctx.DrawRoundedRectangle(
                        brush, pen,
                        new Rect(boxX, boxY, BoxSize, BoxSize),
                        2, 2);

                    // Draw a coloured underline spanning the full token
                    double lineY = rect.Bottom - SwatchH - 1;
                    ctx.DrawRectangle(
                        brush, null,
                        new Rect(rect.Left, lineY, rect.Width, SwatchH));

                    break;
                }
            }
        }
    }
}