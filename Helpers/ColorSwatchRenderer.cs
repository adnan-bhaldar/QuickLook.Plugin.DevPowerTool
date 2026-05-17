// ============================================================
// QuickLook.Plugin.DevPowerTool — Helpers/ColorSwatchRenderer.cs
// AvalonEdit IBackgroundRenderer that draws small colour squares
// inline in the editor at detected colour token positions.
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
        public int   Line;       // 1-based
        public int   CharOffset; // char index within the line
        public Color Color;
    }

    public sealed class ColorSwatchRenderer : IBackgroundRenderer
    {
        private const double Size   = 10;
        private const double Radius = 2;
        private const double Gap    = 2;

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
                var seg = new ICSharpCode.AvalonEdit.Document.TextSegment
                {
                    StartOffset = docLine.Offset + s.CharOffset,
                    EndOffset   = docLine.Offset + s.CharOffset
                };

                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, seg))
                {
                    double x = rect.Left - Size - Gap;
                    double y = rect.Top + (rect.Height - Size) / 2.0;
                    if (x < 1) x = 1;

                    bool isDark = (0.299 * s.Color.R + 0.587 * s.Color.G + 0.114 * s.Color.B) < 128;
                    var borderColor = isDark
                        ? Color.FromArgb(80, 220, 220, 220)
                        : Color.FromArgb(80, 30, 30, 30);

                    ctx.DrawRoundedRectangle(
                        new SolidColorBrush(s.Color),
                        new Pen(new SolidColorBrush(borderColor), 0.8),
                        new Rect(x, y, Size, Size),
                        Radius, Radius);
                    break;
                }
            }
        }
    }
}