// ============================================================
// QuickLook.Plugin.DevPowerTool — Helpers/ColorSwatchRenderer.cs
//
// AvalonEdit IBackgroundRenderer that draws small filled colour
// squares inline in the editor — one per detected colour token.
// Works for both CSS colour values AND Tailwind class names.
// ============================================================

using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace QuickLook.Plugin.DevPowerTool
{
    /// <summary>One swatch to render: document line number + colour.</summary>
    public sealed class SwatchInfo
    {
        /// <summary>1-based document line number.</summary>
        public int   Line;
        /// <summary>Character offset within the line where the token starts.</summary>
        public int   CharOffset;
        /// <summary>The colour to display.</summary>
        public Color Color;
    }

    /// <summary>
    /// Draws colour swatches directly in AvalonEdit's text layer.
    /// Registered via editor.TextArea.TextView.BackgroundRenderers.Add(this).
    /// </summary>
    public sealed class ColorSwatchRenderer : IBackgroundRenderer
    {
        private const double Size      = 11;
        private const double Radius    = 2.5;
        private const double Gap       = 3;   // pixels between swatch and token text

        private readonly TextEditor       _editor;
        private readonly List<SwatchInfo> _swatches;

        public ColorSwatchRenderer(TextEditor editor, List<SwatchInfo> swatches)
        {
            _editor   = editor;
            _swatches = swatches;
        }

        // Render on the Text layer so swatches appear between line-number margin and text
        public KnownLayer Layer => KnownLayer.Text;

        public void Draw(TextView textView, DrawingContext ctx)
        {
            if (_swatches == null || _swatches.Count == 0) return;

            textView.EnsureVisualLines();

            foreach (var s in _swatches)
            {
                if (s.Line < 1 || s.Line > _editor.Document.LineCount) continue;

                var docLine = _editor.Document.GetLineByNumber(s.Line);

                // Build a zero-length segment at the start of the token
                var seg = new ICSharpCode.AvalonEdit.Document.TextSegment
                {
                    StartOffset = docLine.Offset + s.CharOffset,
                    EndOffset   = docLine.Offset + s.CharOffset
                };

                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, seg))
                {
                    double x = rect.Left - Size - Gap;
                    double y = rect.Top + (rect.Height - Size) / 2.0;

                    if (x < 0) x = 1;

                    var swatchRect = new Rect(x, y, Size, Size);

                    // Thin contrast border so swatch is visible on any background
                    bool dark = (0.299 * s.Color.R + 0.587 * s.Color.G + 0.114 * s.Color.B) < 128;
                    var borderColor = dark
                        ? Color.FromArgb(90, 220, 220, 220)
                        : Color.FromArgb(90, 30,  30,  30);

                    ctx.DrawRoundedRectangle(
                        new SolidColorBrush(s.Color),
                        new Pen(new SolidColorBrush(borderColor), 0.8),
                        swatchRect, Radius, Radius);

                    break; // only first visual rect per line
                }
            }
        }
    }
}