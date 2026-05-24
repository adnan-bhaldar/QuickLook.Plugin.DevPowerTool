using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace QuickLook.Plugin.DevPowerTool.Helpers
{
    public sealed class ColorToken
    {
        public int    Index { get; set; }
        public string Raw   { get; set; }
        public Color? Color { get; set; }
    }

    public static class ColorParser
    {
        private static readonly Regex RxHex = new Regex(
            @"#([0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RxRgb = new Regex(
            @"rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RxRgba = new Regex(
            @"rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*([\d.]+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RxHsl = new Regex(
            @"hsl\(\s*(\d{1,3})\s*,\s*([\d.]+)%\s*,\s*([\d.]+)%\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RxHsla = new Regex(
            @"hsla\(\s*(\d{1,3})\s*,\s*([\d.]+)%\s*,\s*([\d.]+)%\s*,\s*([\d.]+)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<ColorToken> ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return new List<ColorToken>(0);

            var tokens = new List<ColorToken>();
            AddHex(line, tokens);
            AddRgb(line, tokens);
            AddRgba(line, tokens);
            AddHsl(line, tokens);
            AddHsla(line, tokens);
            tokens.Sort((a, b) => a.Index.CompareTo(b.Index));
            return tokens;
        }

        private static void AddHex(string line, List<ColorToken> list)
        {
            foreach (Match m in RxHex.Matches(line))
                list.Add(new ColorToken { Index = m.Index, Raw = m.Value, Color = ParseHex(m.Groups[1].Value) });
        }

        private static void AddRgb(string line, List<ColorToken> list)
        {
            foreach (Match m in RxRgb.Matches(line))
            {
                if (!TryByte(m.Groups[1].Value, out byte r)) continue;
                if (!TryByte(m.Groups[2].Value, out byte g)) continue;
                if (!TryByte(m.Groups[3].Value, out byte b)) continue;
                list.Add(new ColorToken { Index = m.Index, Raw = m.Value, Color = System.Windows.Media.Color.FromRgb(r, g, b) });
            }
        }

        private static void AddRgba(string line, List<ColorToken> list)
        {
            foreach (Match m in RxRgba.Matches(line))
            {
                if (!TryByte(m.Groups[1].Value, out byte r)) continue;
                if (!TryByte(m.Groups[2].Value, out byte g)) continue;
                if (!TryByte(m.Groups[3].Value, out byte b)) continue;
                if (!double.TryParse(m.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double a)) continue;
                byte ab = (byte)Math.Round(Math.Max(0, Math.Min(1, a)) * 255);
                list.Add(new ColorToken { Index = m.Index, Raw = m.Value, Color = System.Windows.Media.Color.FromArgb(ab, r, g, b) });
            }
        }

        private static void AddHsl(string line, List<ColorToken> list)
        {
            foreach (Match m in RxHsl.Matches(line))
            {
                if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double h)) continue;
                if (!double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double s)) continue;
                if (!double.TryParse(m.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double l)) continue;
                list.Add(new ColorToken { Index = m.Index, Raw = m.Value, Color = HslToColor(h, s / 100.0, l / 100.0, 1.0) });
            }
        }

        private static void AddHsla(string line, List<ColorToken> list)
        {
            foreach (Match m in RxHsla.Matches(line))
            {
                if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double h)) continue;
                if (!double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double s)) continue;
                if (!double.TryParse(m.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double l)) continue;
                if (!double.TryParse(m.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double a)) continue;
                list.Add(new ColorToken { Index = m.Index, Raw = m.Value, Color = HslToColor(h, s / 100.0, l / 100.0, a) });
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
                        return System.Windows.Media.Color.FromRgb(
                            HexByte(hex[0], hex[0]), HexByte(hex[1], hex[1]), HexByte(hex[2], hex[2]));
                    case 6:
                        return System.Windows.Media.Color.FromRgb(
                            HexByte(hex[0], hex[1]), HexByte(hex[2], hex[3]), HexByte(hex[4], hex[5]));
                    case 8:
                        return System.Windows.Media.Color.FromArgb(
                            HexByte(hex[6], hex[7]), HexByte(hex[0], hex[1]),
                            HexByte(hex[2], hex[3]), HexByte(hex[4], hex[5]));
                }
            }
            catch { }
            return null;
        }

        private static byte HexByte(char hi, char lo) => (byte)((Nibble(hi) << 4) | Nibble(lo));

        private static int Nibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            throw new FormatException();
        }

        private static bool TryByte(string s, out byte v)
        {
            if (int.TryParse(s, out int i) && i >= 0 && i <= 255) { v = (byte)i; return true; }
            v = 0; return false;
        }

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
                double hn = h / 360.0;
                r = Hue2Rgb(p, q, hn + 1.0 / 3.0);
                g = Hue2Rgb(p, q, hn);
                b = Hue2Rgb(p, q, hn - 1.0 / 3.0);
            }
            byte a = (byte)Math.Round(Math.Max(0, Math.Min(1, alpha)) * 255);
            return System.Windows.Media.Color.FromArgb(a,
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
