// ============================================================
// QuickLook.Plugin.DevPowerTool — Helpers/ColorSwatchRenderer.cs
//
// AvalonEdit IBackgroundRenderer that draws small filled squares
// ("swatches") directly on the editor canvas, immediately to the
// left of each detected colour token.
//
// Why IBackgroundRenderer (not IVisualLineTransformer)?
//   Background renderers receive a DrawingContext on every render
//   pass, making them the lightest-weight option.  They do not
//   reflow text, which means text selection and copy behaviour
//   are completely unaffected — the swatches are purely cosmetic
//   overlays drawn in the Background layer.
//
// Architecture mirrors QuickLook.Plugin.TextViewer's own
// background-rendering extensions.
// ============================================================
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace QuickLook.Plugin.DevPowerTool.Helpers
{
    /// <summary>
    /// Describes one swatch to be rendered: its document position and colour.
    /// </summary>
    public sealed class SwatchInfo
    {
        /// <summary>1-based line number in the document.</summary>
        public int Line { get; set; }

        /// <summary>0-based character offset within that line.</summary>
        public int CharOffset { get; set; }

        /// <summary>Number of characters in the colour token (e.g. 7 for "#ff0000").</summary>
        public int TokenLength { get; set; }

        /// <summary>The colour to display.</summary>
        public Color Color { get; set; }
    }

    /// <summary>
    /// AvalonEdit <see cref="IBackgroundRenderer"/> that paints inline colour
    /// swatches behind the colour token text.
    /// </summary>
    public sealed class ColorSwatchRenderer : IBackgroundRenderer
    {
        // Swatch geometry constants
        private const double SwatchSize   = 10.0; // square side length in device-independent pixels
        private const double SwatchRadius =  2.0; // corner radius
        private const double SwatchGap    =  2.0; // gap between swatch and token text

        private readonly TextEditor       _editor;
        private readonly List<SwatchInfo> _swatches;

        // One frozen pen per unique colour (border around the swatch)
        private readonly Dictionary<Color, Pen> _borderPens = new Dictionary<Color, Pen>();

        public ColorSwatchRenderer(TextEditor editor, List<SwatchInfo> swatches)
        {
            _editor   = editor   ?? throw new ArgumentNullException("editor");
            _swatches = swatches ?? throw new ArgumentNullException("swatches");
        }

        // ── IBackgroundRenderer ───────────────────────────────────────────

        /// <summary>Draw in the Background layer so text renders on top.</summary>
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView == null || drawingContext == null) return;
            if (_swatches.Count == 0) return;

            textView.EnsureVisualLines();

            var doc         = _editor.Document;
            var visualLines = textView.VisualLines;

            if (doc == null || visualLines.Count == 0) return;

            foreach (var swatch in _swatches)
            {
                try
                {
                    DrawSwatch(textView, drawingContext, doc, swatch);
                }
                catch
                {
                    // Never let a rendering exception crash QuickLook
                }
            }
        }

        // ── Private helpers ───────────────────────────────────────────────

        private void DrawSwatch(
            TextView       textView,
            DrawingContext dc,
            ICSharpCode.AvalonEdit.Document.TextDocument doc,
            SwatchInfo     swatch)
        {
            // Validate line number
            if (swatch.Line < 1 || swatch.Line > doc.LineCount) return;

            var docLine   = doc.GetLineByNumber(swatch.Line);
            int absOffset = docLine.Offset + swatch.CharOffset;

            if (absOffset < 0 || absOffset >= doc.TextLength) return;

            // Map document offset → visual position
            var visualLine = textView.GetVisualLine(swatch.Line);
            if (visualLine == null) return;

            // Get the X position of the token start in the visual line
            double tokenX  = visualLine.GetVisualColumn(absOffset - docLine.Offset)
                             * _editor.TextArea.TextView.WideSpaceWidth;

            // AvalonEdit exposes character widths through visual columns;
            // use the text area's font metrics to approximate character width
            double charWidth = _editor.TextArea.TextView.WideSpaceWidth;

            // Y centre of the visual line
            double lineTop    = visualLine.VisualTop - textView.VerticalOffset;
            double lineHeight = visualLine.Height;
            double swatchY    = lineTop + (lineHeight - SwatchSize) / 2.0;

            // Place the swatch immediately before the token text
            double swatchX = tokenX - SwatchSize - SwatchGap;
            if (swatchX < 0) swatchX = 0;

            // Account for horizontal scroll
            swatchX -= textView.HorizontalOffset;

            // Check visibility
            double viewWidth  = textView.ActualWidth;
            double viewHeight = textView.ActualHeight;
            if (swatchX > viewWidth  || swatchX + SwatchSize < 0) return;
            if (swatchY > viewHeight || swatchY + SwatchSize < 0) return;

            var rect = new Rect(swatchX, swatchY, SwatchSize, SwatchSize);

            // Fill with the token colour (full opacity regardless of alpha — so the
            // swatch is always clearly visible)
            var fillColor = System.Windows.Media.Color.FromRgb(
                swatch.Color.R, swatch.Color.G, swatch.Color.B);
            var fillBrush = new SolidColorBrush(fillColor);
            fillBrush.Freeze();

            // Border pen — slightly darker than fill for contrast
            if (!_borderPens.TryGetValue(fillColor, out var pen))
            {
                var borderColor = DarkenColor(fillColor, 0.3);
                pen = new Pen(new SolidColorBrush(borderColor), 0.75);
                pen.Freeze();
                _borderPens[fillColor] = pen;
            }

            var geometry = new RectangleGeometry(rect, SwatchRadius, SwatchRadius);
            geometry.Freeze();

            dc.DrawGeometry(fillBrush, pen, geometry);
        }

        /// <summary>
        /// Darkens a colour by blending it toward black by <paramref name="amount"/> (0–1).
        /// </summary>
        private static Color DarkenColor(Color c, double amount)
        {
            double factor = 1.0 - Math.Max(0, Math.Min(1, amount));
            return System.Windows.Media.Color.FromRgb(
                (byte)(c.R * factor),
                (byte)(c.G * factor),
                (byte)(c.B * factor));
        }
    }
}
