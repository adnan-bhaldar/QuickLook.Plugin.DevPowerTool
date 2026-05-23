// ============================================================
// QuickLook.Plugin.DevPowerTool — Helpers/ColorParser.cs
//
// Stateless colour token extractor.
// All five regex patterns are compiled once at class load time
// (RegexOptions.Compiled) so repeated per-line calls are fast.
//
// Supported formats
//   #rgb            e.g. #fff
//   #rrggbb         e.g. #1a2b3c
//   #rrggbbaa       e.g. #1a2b3cff
//   rgb(r,g,b)
//   rgba(r,g,b,a)
//   hsl(h,s%,l%)
//   hsla(h,s%,l%,a)
// ============================================================
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace QuickLook.Plugin.DevPowerTool.Helpers
{
    /// <summary>
    /// A single colour token found inside one line of source text.
    /// </summary>
    public sealed class ColorToken
    {
        /// <summary>Zero-based character index within the line.</summary>
        public int Index { get; set; }

        /// <summary>The raw matched string (e.g. "#ff0000" or "rgba(255,0,0,1)").</summary>
        public string Raw { get; set; }

        /// <summary>Parsed WPF colour, or null if parsing failed.</summary>
        public Color? Color { get; set; }
    }

    /// <summary>
    /// Parses one line of text and returns every colour token it contains.
    /// Thread-safe — all state is in compiled static regexes.
    /// </summary>
    public static class ColorParser
    {
        // ── Compiled regexes ──────────────────────────────────────────────

        // #rrggbbaa / #rrggbb / #rgb  (case-insensitive)
        private static readonly Regex RxHex = new Regex(
            @"#([0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // rgb(r, g, b)  — integers or percentages
        private static readonly Regex RxRgb = new Regex(
            @"rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // rgba(r, g, b, a)
        private static readonly Regex RxRgba = new Regex(
            @"rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*([\d.]+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // hsl(h, s%, l%)
        private static readonly Regex RxHsl = new Regex(
            @"hsl\(\s*(\d{1,3})\s*,\s*([\d.]+)%\s*,\s*([\d.]+)%\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // hsla(h, s%, l%, a)
        private static readonly Regex RxHsla = new Regex(
            @"hsla\(\s*(\d{1,3})\s*,\s*([\d.]+)%\s*,\s*([\d.]+)%\s*,\s*([\d.]+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Parses <paramref name="line"/> and returns one <see cref="ColorToken"/>
        /// per detected colour value, in left-to-right order.
        /// Returns an empty list when no colours are found.
        /// </summary>
        public static List<ColorToken> ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return new List<ColorToken>(0);

            var tokens = new List<ColorToken>();

            AddHexMatches(line, tokens);
            AddRgbMatches(line, tokens);
            AddRgbaMatches(line, tokens);
            AddHslMatches(line, tokens);
            AddHslaMatches(line, tokens);

            // Sort by position so callers get tokens left-to-right
            tokens.Sort((a, b) => a.Index.CompareTo(b.Index));

            return tokens;
        }

        // ── Private match helpers ─────────────────────────────────────────

        private static void AddHexMatches(string line, List<ColorToken> tokens)
        {
            foreach (Match m in RxHex.Matches(line))
            {
                var color = ParseHex(m.Groups[1].Value);
                tokens.Add(new ColorToken
                {
                    Index = m.Index,
                    Raw   = m.Value,
                    Color = color
                });
            }
        }

        private static void AddRgbMatches(string line, List<ColorToken> tokens)
        {
            foreach (Match m in RxRgb.Matches(line))
            {
                if (!TryParseInt(m.Groups[1].Value, out byte r)) continue;
                if (!TryParseInt(m.Groups[2].Value, out byte g)) continue;
                if (!TryParseInt(m.Groups[3].Value, out byte b)) continue;

                tokens.Add(new ColorToken
                {
                    Index = m.Index,
                    Raw   = m.Value,
                    Color = System.Windows.Media.Color.FromRgb(r, g, b)
                });
            }
        }

        private static void AddRgbaMatches(string line, List<ColorToken> tokens)
        {
            foreach (Match m in RxRgba.Matches(line))
            {
                if (!TryParseInt(m.Groups[1].Value, out byte r)) continue;
                if (!TryParseInt(m.Groups[2].Value, out byte g)) continue;
                if (!TryParseInt(m.Groups[3].Value, out byte b)) continue;
                if (!double.TryParse(m.Groups[4].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double alpha)) continue;

                byte a = (byte)Math.Round(Math.Max(0, Math.Min(1, alpha)) * 255);

                tokens.Add(new ColorToken
                {
                    Index = m.Index,
                    Raw   = m.Value,
                    Color = System.Windows.Media.Color.FromArgb(a, r, g, b)
                });
            }
        }

        private static void AddHslMatches(string line, List<ColorToken> tokens)
        {
            foreach (Match m in RxHsl.Matches(line))
            {
                if (!double.TryParse(m.Groups[1].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double h)) continue;
                if (!double.TryParse(m.Groups[2].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double s)) continue;
                if (!double.TryParse(m.Groups[3].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double l)) continue;

                var color = HslToColor(h, s / 100.0, l / 100.0, 1.0);
                tokens.Add(new ColorToken
                {
                    Index = m.Index,
                    Raw   = m.Value,
                    Color = color
                });
            }
        }

        private static void AddHslaMatches(string line, List<ColorToken> tokens)
        {
            foreach (Match m in RxHsla.Matches(line))
            {
                if (!double.TryParse(m.Groups[1].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double h)) continue;
                if (!double.TryParse(m.Groups[2].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double s)) continue;
                if (!double.TryParse(m.Groups[3].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double l)) continue;
                if (!double.TryParse(m.Groups[4].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double alpha)) continue;

                var color = HslToColor(h, s / 100.0, l / 100.0, alpha);
                tokens.Add(new ColorToken
                {
                    Index = m.Index,
                    Raw   = m.Value,
                    Color = color
                });
            }
        }

        // ── Colour conversion helpers ─────────────────────────────────────

        /// <summary>
        /// Parses a hex colour component string (3, 6, or 8 hex digits, no leading #).
        /// Returns null on parse failure.
        /// </summary>
        private static Color? ParseHex(string hex)
        {
            try
            {
                switch (hex.Length)
                {
                    case 3:
                        // Expand shorthand: "abc" → "aabbcc"
                        byte r3 = HexByte(hex[0], hex[0]);
                        byte g3 = HexByte(hex[1], hex[1]);
                        byte b3 = HexByte(hex[2], hex[2]);
                        return System.Windows.Media.Color.FromRgb(r3, g3, b3);

                    case 6:
                        byte r6 = HexByte(hex[0], hex[1]);
                        byte g6 = HexByte(hex[2], hex[3]);
                        byte b6 = HexByte(hex[4], hex[5]);
                        return System.Windows.Media.Color.FromRgb(r6, g6, b6);

                    case 8:
                        byte r8 = HexByte(hex[0], hex[1]);
                        byte g8 = HexByte(hex[2], hex[3]);
                        byte b8 = HexByte(hex[4], hex[5]);
                        byte a8 = HexByte(hex[6], hex[7]);
                        return System.Windows.Media.Color.FromArgb(a8, r8, g8, b8);
                }
            }
            catch { /* fall through */ }
            return null;
        }

        private static byte HexByte(char hi, char lo)
            => (byte)((HexNibble(hi) << 4) | HexNibble(lo));

        private static int HexNibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            throw new FormatException("Invalid hex char: " + c);
        }

        private static bool TryParseInt(string s, out byte value)
        {
            if (int.TryParse(s, out int i) && i >= 0 && i <= 255)
            {
                value = (byte)i;
                return true;
            }
            value = 0;
            return false;
        }

        /// <summary>
        /// Converts HSL (hue 0–360, saturation 0–1, lightness 0–1, alpha 0–1)
        /// to a WPF <see cref="Color"/>.
        /// </summary>
        private static Color HslToColor(double h, double s, double l, double alpha)
        {
            double r, g, b;

            if (Math.Abs(s) < 1e-10)
            {
                r = g = b = l; // achromatic
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                double hNorm = h / 360.0;
                r = Hue2Rgb(p, q, hNorm + 1.0 / 3.0);
                g = Hue2Rgb(p, q, hNorm);
                b = Hue2Rgb(p, q, hNorm - 1.0 / 3.0);
            }

            byte a = (byte)Math.Round(Math.Max(0, Math.Min(1, alpha)) * 255);
            return System.Windows.Media.Color.FromArgb(
                a,
                (byte)Math.Round(Math.Max(0, Math.Min(1, r)) * 255),
                (byte)Math.Round(Math.Max(0, Math.Min(1, g)) * 255),
                (byte)Math.Round(Math.Max(0, Math.Min(1, b)) * 255));
        }

        private static double Hue2Rgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }
    }
}
