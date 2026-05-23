// ============================================================
// QuickLook.Plugin.DevPowerTool — Helpers/EnvMaskingService.cs
//
// Pure parser for .env files.
// Converts raw .env text into a list of EnvLine objects.
// No I/O, no side effects, no file access.
//
// .env line grammar (subset, matches dotenv convention):
//   comment line   → starts with optional whitespace then '#'
//   blank line     → empty or only whitespace
//   assignment     → KEY=VALUE  or  KEY="VALUE"  or  export KEY=VALUE
//   continuation   → anything not matching the above → treated as PlainText
// ============================================================
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QuickLook.Plugin.DevPowerTool.Helpers
{
    /// <summary>Kind of a single line in a .env file.</summary>
    public enum EnvLineKind
    {
        /// <summary>A KEY=VALUE assignment.</summary>
        Assignment,

        /// <summary>A comment line beginning with #.</summary>
        Comment,

        /// <summary>A blank or whitespace-only line.</summary>
        Blank,

        /// <summary>A line that doesn't match any known pattern.</summary>
        PlainText
    }

    /// <summary>
    /// Represents one parsed line from a .env file together with its
    /// masking / reveal logic.
    /// </summary>
    public sealed class EnvLine
    {
        /// <summary>Original raw text of the line (no newline character).</summary>
        public string Raw { get; set; }

        public EnvLineKind Kind { get; set; }

        /// <summary>Variable name (only set for <see cref="EnvLineKind.Assignment"/>).</summary>
        public string Key { get; set; }

        /// <summary>Raw value (only set for <see cref="EnvLineKind.Assignment"/>).</summary>
        public string Value { get; set; }

        /// <summary>
        /// Leading whitespace / "export " prefix before the key, if any.
        /// Preserved so the original formatting is reconstructed exactly.
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// Returns the display text for this line.
        /// For assignments, the value is either masked or revealed
        /// based on <paramref name="reveal"/>.
        /// All other line kinds return their raw text unchanged.
        /// </summary>
        public string DisplayText(bool reveal)
        {
            if (Kind != EnvLineKind.Assignment)
                return Raw;

            var displayValue = reveal ? Value : MaskValue(Value);
            return Prefix + Key + "=" + displayValue;
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static string MaskValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // Preserve quotes if value is quoted, masking only the inner content
            if (value.Length >= 2)
            {
                char first = value[0];
                char last  = value[value.Length - 1];

                if ((first == '"'  && last == '"')  ||
                    (first == '\'' && last == '\'') ||
                    (first == '`'  && last == '`'))
                {
                    // e.g. "my-secret" → "********"
                    return first + new string('*', Math.Max(8, value.Length - 2)) + last;
                }
            }

            return new string('*', Math.Max(8, value.Length));
        }
    }

    /// <summary>
    /// Parses raw .env text into a list of <see cref="EnvLine"/> records.
    /// </summary>
    public static class EnvMaskingService
    {
        // Matches: [optional whitespace] [optional "export "] KEY = VALUE
        // Groups:  1 = prefix (whitespace + "export "), 2 = KEY, 3 = VALUE
        private static readonly Regex RxAssignment = new Regex(
            @"^(\s*(?:export\s+)?)([A-Za-z_][A-Za-z0-9_]*)=(.*)$",
            RegexOptions.Compiled);

        // Comment: optional whitespace then '#'
        private static readonly Regex RxComment = new Regex(
            @"^\s*#",
            RegexOptions.Compiled);

        /// <summary>
        /// Parses <paramref name="rawText"/> and returns one <see cref="EnvLine"/>
        /// per line.  The original line endings are normalised to \n internally
        /// but the Raw property stores each line without any trailing newline.
        /// </summary>
        public static List<EnvLine> Parse(string rawText)
        {
            if (rawText == null) throw new ArgumentNullException("rawText");

            var lines  = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var result = new List<EnvLine>(lines.Length);

            foreach (var line in lines)
            {
                result.Add(ParseLine(line));
            }

            return result;
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static EnvLine ParseLine(string line)
        {
            // Blank
            if (string.IsNullOrWhiteSpace(line))
                return new EnvLine { Raw = line, Kind = EnvLineKind.Blank };

            // Comment
            if (RxComment.IsMatch(line))
                return new EnvLine { Raw = line, Kind = EnvLineKind.Comment };

            // Assignment
            var m = RxAssignment.Match(line);
            if (m.Success)
            {
                return new EnvLine
                {
                    Raw    = line,
                    Kind   = EnvLineKind.Assignment,
                    Prefix = m.Groups[1].Value,
                    Key    = m.Groups[2].Value,
                    Value  = m.Groups[3].Value
                };
            }

            // Fallback — plain text (multi-line values, continuation lines, etc.)
            return new EnvLine { Raw = line, Kind = EnvLineKind.PlainText };
        }
    }
}
