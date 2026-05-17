// ============================================================
// QuickLook.Plugin.DevPowerTool — Helpers/ColorSwatchRenderer.cs
//
// Draws a small colour box immediately before each colour token.
// Gets margin width directly from the editor's TextArea children
// to avoid non-existent AvalonEdit 6.x API calls.
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

        // Cached margin width — measured once on first Draw call
        private double _marginWidth = -1;

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

            // Measure margin width once: find the TextView inside the TextArea
            // and use its offset from the TextArea left edge.
            if (_marginWidth < 0)
            {
                try
                {
                    // The TextView's translation relative to the TextArea gives the margin width
                    var transform = textView.TransformToAncestor(_editor.TextArea);
                    var origin    = transform.Transform(new Point(0, 0));
                    _marginWidth  = origin.X;
                }
                catch
                {
                    _marginWidth = 40; // safe fallback
                }
            }

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

                // Don't draw inside the line number margin
                if (boxX < _marginWidth - BoxSize) continue;

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