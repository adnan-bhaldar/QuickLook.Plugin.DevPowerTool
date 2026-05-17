// ============================================================
// QuickLook.Plugin.DevPowerTool — Helpers/ColorSwatchRenderer.cs
//
// AvalonEdit IBackgroundRenderer that draws colour swatches.
// Uses a non-zero text segment (the token itself) so AvalonEdit
// can correctly calculate the pixel X position for the swatch.
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
        public int   Line;        // 1-based document line number
        public int   CharOffset;  // character index within the line where token starts
        public int   TokenLength; // length of the colour token text
        public Color Color;
    }

    public sealed class ColorSwatchRenderer : IBackgroundRenderer
    {
        private const double SwatchSize   = 11;
        private const double SwatchRadius = 2.5;
        private const double Gap          = 2; // pixels between swatch right edge and token

        private readonly TextEditor       _editor;
        private readonly List<SwatchInfo> _swatches;

        public ColorSwatchRenderer(TextEditor editor, List<SwatchInfo> swatches)
        {
            _editor   = editor;
            _swatches = swatches;
        }

        public KnownLayer Layer => KnownLayer.Text;

        public void Draw(TextView textView, DrawingContext ctx)
        {
            if (_swatches == null || _swatches.Count == 0) return;
            textView.EnsureVisualLines();

            foreach (var s in _swatches)
            {
                if (s.Line < 1 || s.Line > _editor.Document.LineCount) continue;

                var docLine = _editor.Document.GetLineByNumber(s.Line);

                // Use the actual token as the segment so we get the correct X position
                int tokenStart  = docLine.Offset + s.CharOffset;
                int tokenEnd    = tokenStart + (s.TokenLength > 0 ? s.TokenLength : 1);

                // Clamp to line bounds
                if (tokenStart >= docLine.EndOffset) continue;
                if (tokenEnd   >  docLine.EndOffset)
                    tokenEnd = docLine.EndOffset;

                var seg = new ICSharpCode.AvalonEdit.Document.TextSegment
                {
                    StartOffset = tokenStart,
                    EndOffset   = tokenEnd
                };

                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, seg))
                {
                    // rect.Left is the pixel X of the token start — place swatch just before it
                    double x = rect.Left - SwatchSize - Gap;
                    double y = rect.Top + (rect.Height - SwatchSize) / 2.0;

                    if (x < 0) x = 0;

                    bool isDark = (0.299 * s.Color.R + 0.587 * s.Color.G + 0.114 * s.Color.B) < 128;
                    var borderColor = isDark
                        ? Color.FromArgb(80, 220, 220, 220)
                        : Color.FromArgb(80, 30,  30,  30);

                    ctx.DrawRoundedRectangle(
                        new SolidColorBrush(s.Color),
                        new Pen(new SolidColorBrush(borderColor), 0.8),
                        new Rect(x, y, SwatchSize, SwatchSize),
                        SwatchRadius, SwatchRadius);

                    break; // only first rect per token
                }
            }
        }
    }
}