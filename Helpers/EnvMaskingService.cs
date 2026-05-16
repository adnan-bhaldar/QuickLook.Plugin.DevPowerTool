// ============================================================
// QuickLook.Plugin.DevPowerTool — EnvMaskingService.cs
// Parses .env files and provides masked / revealed line views.
// Never touches the original file on disk.
// ============================================================

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QuickLook.Plugin.DevPowerTool
{
    /// <summary>
    /// Represents one parsed line from an .env file.
    /// </summary>
    public sealed class EnvLine
    {
        /// <summary>The original raw text of the line.</summary>
        public string Raw { get; set; }

        /// <summary>
        /// True when the line is a KEY=VALUE assignment (not a comment or blank).
        /// </summary>
        public bool IsAssignment { get; set; }

        /// <summary>Key portion, e.g. "DATABASE_URL". Null for non-assignment lines.</summary>
        public string Key { get; set; }

        /// <summary>The real value (kept in memory only, never written anywhere).</summary>
        public string Value { get; set; }

        /// <summary>Returns the display text given the current reveal state.</summary>
        public string DisplayText(bool revealed)
        {
            if (!IsAssignment)
                return Raw; // comments, blanks — always shown as-is

            if (revealed)
                return Raw;

            // Mask: show KEY=******** preserving the surrounding quote style
            var mask = new string('*', Math.Max(8, Value?.Length ?? 8));
            return $"{Key}={mask}";
        }
    }

    /// <summary>
    /// Parses the raw text of an .env file into a list of <see cref="EnvLine"/>
    /// objects. Masking is purely in-memory.
    /// </summary>
    public static class EnvMaskingService
    {
        // Matches: KEY=VALUE  or  KEY="VALUE"  or  KEY='VALUE'
        // Group 1 = key, Group 2 = full value (including any surrounding quotes)
        private static readonly Regex AssignmentRegex = new Regex(
            @"^([A-Za-z_][A-Za-z0-9_]*)=(.*)",
            RegexOptions.Compiled);

        /// <summary>
        /// Parses every line in <paramref name="rawText"/> and returns an ordered
        /// list of <see cref="EnvLine"/> objects.
        /// </summary>
        public static List<EnvLine> Parse(string rawText)
        {
            var lines  = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var result = new List<EnvLine>(lines.Length);

            foreach (var raw in lines)
            {
                var trimmed = raw.Trim();

                // Blank line or comment
                if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                {
                    result.Add(new EnvLine { Raw = raw, IsAssignment = false });
                    continue;
                }

                var m = AssignmentRegex.Match(trimmed);
                if (m.Success)
                {
                    result.Add(new EnvLine
                    {
                        Raw          = raw,
                        IsAssignment = true,
                        Key          = m.Groups[1].Value,
                        Value        = m.Groups[2].Value
                    });
                }
                else
                {
                    // Export / other directives — treat as non-assignment
                    result.Add(new EnvLine { Raw = raw, IsAssignment = false });
                }
            }

            return result;
        }
    }
}
