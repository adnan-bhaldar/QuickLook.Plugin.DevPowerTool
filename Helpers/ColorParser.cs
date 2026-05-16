// ============================================================
// QuickLook.Plugin.DevPowerTool — ColorParser.cs
// Detects CSS color tokens in a line of text using compiled
// regular expressions for best performance on large files.
// ============================================================

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace QuickLook.Plugin.DevPowerTool
{
    /// <summary>
    /// Represents one colour token found inside a line of source text.
    /// </summary>
    public sealed class ColorToken
    {
        /// <summary>Zero-based char index where the token starts in the line.</summary>
        public int Index { get; set; }

        /// <summary>Raw text of the colour token, e.g. "#ff6600" or "rgb(255,0,0)".</summary>
        public string Raw { get; set; }

        /// <summary>Parsed WPF colour, or null if parsing failed.</summary>
        public Color? Color { get; set; }
    }

    /// <summary>
    /// Stateless utility that extracts colour tokens from a line of text.
    /// All regexes are pre-compiled at class initialisation time.
    /// </summary>
    public static class ColorParser
    {
        // ── Compiled regexes ──────────────────────────────────────────────

        // Hex: #rgb, #rrggbb, #rrggbbaa  (case-insensitive)
        private static readonly Regex HexRegex = new Regex(
            @"#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3,4})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // rgb(r, g, b) — integers 0-255
        private static readonly Regex RgbRegex = new Regex(
            @"rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // rgba(r, g, b, a) — a is 0.0-1.0
        private static readonly Regex RgbaRegex = new Regex(
            @"rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*([\d.]+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // hsl(h, s%, l%)
        private static readonly Regex HslRegex = new Regex(
            @"hsl\(\s*(\d{1,3})\s*,\s*([\d.]+)%\s*,\s*([\d.]+)%\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // hsla(h, s%, l%, a)
        private static readonly Regex HslaRegex = new Regex(
            @"hsla\(\s*(\d{1,3})\s*,\s*([\d.]+)%\s*,\s*([\d.]+)%\s*,\s*([\d.]+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Returns all colour tokens found in <paramref name="line"/>, sorted
        /// by their start index so callers can split the line sequentially.
        /// </summary>
        public static List<ColorToken> ParseLine(string line)
        {
            var results = new List<ColorToken>();

            ExtractHex(line, results);
            ExtractRgb(line, results);
            ExtractRgba(line, results);
            ExtractHsl(line, results);
            ExtractHsla(line, results);

            // Sort by position so the UI can walk left→right without overlap checks
            results.Sort((a, b) => a.Index.CompareTo(b.Index));

            // Remove overlapping matches (keep the first/left-most)
            return RemoveOverlaps(results);
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static void ExtractHex(string line, List<ColorToken> list)
        {
            foreach (Match m in HexRegex.Matches(line))
            {
                var color = ParseHex(m.Value);
                list.Add(new ColorToken { Index = m.Index, Raw = m.Value, Color = color });
            }
        }

        private static void ExtractRgb(string line, List<ColorToken> list)
        {
            foreach (Match m in RgbRegex.Matches(line))
            {
                if (!TryByte(m.Groups[1].Value, out byte r)) continue;
                if (!TryByte(m.Groups[2].Value, out byte g)) continue;
                if (!TryByte(m.Groups[3].Value, out byte b)) continue;
                list.Add(new ColorToken
                {
                    Index = m.Index, Raw = m.Value,
                    Color = Color.FromRgb(r, g, b)
                });
            }
        }

        private static void ExtractRgba(string line, List<ColorToken> list)
        {
            foreach (Match m in RgbaRegex.Matches(line))
            {
                if (!TryByte(m.Groups[1].Value, out byte r)) continue;
                if (!TryByte(m.Groups[2].Value, out byte g)) continue;
                if (!TryByte(m.Groups[3].Value, out byte b)) continue;
                if (!double.TryParse(m.Groups[4].Value, out double a)) continue;
                var alpha = (byte)Math.Clamp((int)(a * 255), 0, 255);
                list.Add(new ColorToken
                {
                    Index = m.Index, Raw = m.Value,
                    Color = Color.FromArgb(alpha, r, g, b)
                });
            }
        }

        private static void ExtractHsl(string line, List<ColorToken> list)
        {
            foreach (Match m in HslRegex.Matches(line))
            {
                if (!double.TryParse(m.Groups[1].Value, out double h)) continue;
                if (!double.TryParse(m.Groups[2].Value, out double s)) continue;
                if (!double.TryParse(m.Groups[3].Value, out double l)) continue;
                list.Add(new ColorToken
                {
                    Index = m.Index, Raw = m.Value,
                    Color = HslToColor(h, s / 100.0, l / 100.0, 1.0)
                });
            }
        }

        private static void ExtractHsla(string line, List<ColorToken> list)
        {
            foreach (Match m in HslaRegex.Matches(line))
            {
                if (!double.TryParse(m.Groups[1].Value, out double h)) continue;
                if (!double.TryParse(m.Groups[2].Value, out double s)) continue;
                if (!double.TryParse(m.Groups[3].Value, out double l)) continue;
                if (!double.TryParse(m.Groups[4].Value, out double a)) continue;
                list.Add(new ColorToken
                {
                    Index = m.Index, Raw = m.Value,
                    Color = HslToColor(h, s / 100.0, l / 100.0, a)
                });
            }
        }

        private static Color? ParseHex(string hex)
        {
            try
            {
                // Strip leading '#'
                var raw = hex.TrimStart('#');
                byte a = 255, r, g, b;

                switch (raw.Length)
                {
                    case 3: // #rgb → #rrggbb
                        r = Convert.ToByte(new string(raw[0], 2), 16);
                        g = Convert.ToByte(new string(raw[1], 2), 16);
                        b = Convert.ToByte(new string(raw[2], 2), 16);
                        break;
                    case 4: // #rgba
                        r = Convert.ToByte(new string(raw[0], 2), 16);
                        g = Convert.ToByte(new string(raw[1], 2), 16);
                        b = Convert.ToByte(new string(raw[2], 2), 16);
                        a = Convert.ToByte(new string(raw[3], 2), 16);
                        break;
                    case 6: // #rrggbb
                        r = Convert.ToByte(raw.Substring(0, 2), 16);
                        g = Convert.ToByte(raw.Substring(2, 2), 16);
                        b = Convert.ToByte(raw.Substring(4, 2), 16);
                        break;
                    case 8: // #rrggbbaa
                        r = Convert.ToByte(raw.Substring(0, 2), 16);
                        g = Convert.ToByte(raw.Substring(2, 2), 16);
                        b = Convert.ToByte(raw.Substring(4, 2), 16);
                        a = Convert.ToByte(raw.Substring(6, 2), 16);
                        break;
                    default:
                        return null;
                }
                return Color.FromArgb(a, r, g, b);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Converts HSL (hue 0-360, saturation 0-1, lightness 0-1, alpha 0-1)
        /// to a WPF Color using standard formulae.
        /// </summary>
        private static Color HslToColor(double h, double s, double l, double a)
        {
            double r, g, b;
            if (Math.Abs(s) < 1e-9)
            {
                r = g = b = l; // achromatic
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h / 360.0 + 1.0 / 3.0);
                g = HueToRgb(p, q, h / 360.0);
                b = HueToRgb(p, q, h / 360.0 - 1.0 / 3.0);
            }

            return Color.FromArgb(
                (byte)Math.Clamp((int)(a   * 255), 0, 255),
                (byte)Math.Clamp((int)(r   * 255), 0, 255),
                (byte)Math.Clamp((int)(g   * 255), 0, 255),
                (byte)Math.Clamp((int)(b   * 255), 0, 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        private static bool TryByte(string s, out byte value)
        {
            if (byte.TryParse(s, out value)) return true;
            value = 0;
            return false;
        }

        /// <summary>
        /// Removes overlapping tokens keeping the left-most one.
        /// Assumes <paramref name="sorted"/> is already sorted by Index.
        /// </summary>
        private static List<ColorToken> RemoveOverlaps(List<ColorToken> sorted)
        {
            var clean = new List<ColorToken>();
            int endOfLast = 0;
            foreach (var t in sorted)
            {
                if (t.Index >= endOfLast)
                {
                    clean.Add(t);
                    endOfLast = t.Index + t.Raw.Length;
                }
            }
            return clean;
        }
    }
}
