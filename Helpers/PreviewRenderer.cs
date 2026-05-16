// ============================================================
// QuickLook.Plugin.DevPowerTool — PreviewRenderer.cs
// Converts parsed source-file lines into WPF FlowDocument
// paragraphs, injecting inline colour swatches where detected.
// ============================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace QuickLook.Plugin.DevPowerTool
{
    /// <summary>
    /// Builds a WPF <see cref="FlowDocument"/> from source file lines,
    /// injecting inline coloured squares next to detected colour tokens.
    /// </summary>
    public static class PreviewRenderer
    {
        // ── Theming constants ─────────────────────────────────────────────
        // These are set at render time from the calling panel so the UI
        // can pass in colours derived from SystemColors / theme detection.

        /// <summary>
        /// Renders the given lines as a FlowDocument with inline colour swatches.
        /// </summary>
        /// <param name="lines">Source lines to render.</param>
        /// <param name="isDark">True when QuickLook is using a dark theme.</param>
        /// <returns>A populated <see cref="FlowDocument"/>.</returns>
        public static FlowDocument RenderWithSwatches(IReadOnlyList<string> lines, bool isDark)
        {
            var doc = CreateBaseDocument(isDark);

            foreach (var line in lines)
            {
                var para  = CreateParagraph();
                var tokens = ColorParser.ParseLine(line);

                if (tokens.Count == 0)
                {
                    // Fast path — no colours; just add the line as plain text
                    para.Inlines.Add(MakeTextRun(line, isDark));
                }
                else
                {
                    // Walk through segments between colour tokens
                    int cursor = 0;
                    foreach (var token in tokens)
                    {
                        // Text before this token
                        if (token.Index > cursor)
                            para.Inlines.Add(MakeTextRun(line.Substring(cursor, token.Index - cursor), isDark));

                        // Colour swatch (small filled rectangle)
                        if (token.Color.HasValue)
                            para.Inlines.Add(MakeSwatch(token.Color.Value));

                        // The colour token text itself
                        para.Inlines.Add(MakeColorRun(token.Raw, token.Color, isDark));

                        cursor = token.Index + token.Raw.Length;
                    }

                    // Remaining text after last token
                    if (cursor < line.Length)
                        para.Inlines.Add(MakeTextRun(line.Substring(cursor), isDark));
                }

                doc.Blocks.Add(para);
            }

            return doc;
        }

        // ── Private factory helpers ───────────────────────────────────────

        private static FlowDocument CreateBaseDocument(bool isDark)
        {
            return new FlowDocument
            {
                FontFamily        = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                FontSize          = 13,
                LineHeight        = 20,
                PagePadding       = new Thickness(16, 12, 16, 12),
                Background        = new SolidColorBrush(isDark
                                        ? Color.FromRgb(0x1E, 0x1E, 0x2E)
                                        : Color.FromRgb(0xFA, 0xFA, 0xFB)),
                Foreground        = new SolidColorBrush(isDark
                                        ? Color.FromRgb(0xCD, 0xD6, 0xF4)
                                        : Color.FromRgb(0x1E, 0x1E, 0x2E)),
                IsColumnWidthFlexible = false,
                ColumnWidth       = double.MaxValue  // single column, full-width
            };
        }

        private static Paragraph CreateParagraph()
        {
            return new Paragraph
            {
                Margin      = new Thickness(0),
                Padding     = new Thickness(0),
                LineHeight  = 20
            };
        }

        private static Run MakeTextRun(string text, bool isDark)
        {
            return new Run(text)
            {
                Foreground = new SolidColorBrush(isDark
                    ? Color.FromRgb(0xCD, 0xD6, 0xF4)
                    : Color.FromRgb(0x1E, 0x1E, 0x2E))
            };
        }

        /// <summary>
        /// Creates the Run for the colour token text itself,
        /// optionally tinted to visually distinguish colour values.
        /// </summary>
        private static Run MakeColorRun(string text, Color? color, bool isDark)
        {
            // If the swatch colour is very light or very dark adapt the text colour
            // slightly so the token still stands out in syntax-highlight fashion.
            var brush = new SolidColorBrush(isDark
                ? Color.FromRgb(0xA6, 0xE3, 0xA1)   // muted green — catppuccin "green"
                : Color.FromRgb(0x40, 0x8B, 0x3D));  // dark green for light theme

            return new Run(text) { Foreground = brush };
        }

        /// <summary>
        /// Builds a small inline coloured square swatch that appears just before
        /// the colour token text. Uses an InlineUIContainer holding a Border.
        /// </summary>
        private static InlineUIContainer MakeSwatch(Color color)
        {
            // Determine border colour: subtle contrast ring
            var borderColor = IsDark(color)
                ? Color.FromArgb(80, 255, 255, 255)
                : Color.FromArgb(80, 0, 0, 0);

            var border = new System.Windows.Controls.Border
            {
                Width           = 12,
                Height          = 12,
                Margin          = new Thickness(0, 0, 3, -2), // align to baseline
                CornerRadius    = new System.Windows.CornerRadius(2),
                Background      = new SolidColorBrush(color),
                BorderBrush     = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(1),
                ToolTip         = $"#{color.R:X2}{color.G:X2}{color.B:X2}" +
                                  (color.A < 255 ? $"{color.A:X2}" : "")
            };

            return new InlineUIContainer(border);
        }

        /// <summary>
        /// Simple luminance check to decide whether a colour is "dark"
        /// (relative luminance < 0.5) for border contrast.
        /// </summary>
        private static bool IsDark(Color c)
        {
            double lum = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            return lum < 128;
        }
    }
}
