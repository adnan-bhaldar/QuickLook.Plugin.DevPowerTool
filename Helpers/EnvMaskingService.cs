// ============================================================
// QuickLook.Plugin.DevPowerTool — Helpers/EnvMaskingService.cs
//
// Parses .env files and provides masked / revealed line views.
// Never touches the original file on disk.
//
// Masking rules:
//   - Active assignments:   KEY=value       → KEY=********
//   - Commented assignments: # KEY=value    → # KEY=********
//   - Blank lines / pure comments (no =)   → shown as-is
//   - export KEY=value                      → export KEY=********
// ============================================================

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QuickLook.Plugin.DevPowerTool
{
    public sealed class EnvLine
    {
        /// <summary>The original raw text of the line.</summary>
        public string Raw { get; set; }

        /// <summary>True when this line contains a KEY=VALUE that should be masked.</summary>
        public bool HasSecret { get; set; }

        /// <summary>
        /// Everything before the value — e.g. "KEY=" or "# KEY=" or "export KEY=".
        /// Null when HasSecret is false.
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>The real value. Kept in memory only, never written to disk.</summary>
        public string Value { get; set; }

        /// <summary>Returns the display text for this line given the current reveal state.</summary>
        public string DisplayText(bool revealed)
        {
            if (!HasSecret)
                return Raw;

            if (revealed)
                return Raw;

            // Mask the value portion only; preserve prefix (key name, comment marker, etc.)
            string mask = new string('*', Math.Max(8, Value == null ? 8 : Value.Length));
            return Prefix + mask;
        }
    }

    public static class EnvMaskingService
    {
        // Matches active assignments: KEY=value or export KEY=value
        // Captures: Group 1 = everything up to and including "=", Group 2 = value
        private static readonly Regex ActiveRegex = new Regex(
            @"^((?:export\s+)?[A-Za-z_][A-Za-z0-9_]*\s*=)(.*)",
            RegexOptions.Compiled);

        // Matches commented-out assignments: # KEY=value or #KEY=value
        // Captures: Group 1 = everything up to and including "=", Group 2 = value
        private static readonly Regex CommentedRegex = new Regex(
            @"^(#\s*[A-Za-z_][A-Za-z0-9_]*\s*=)(.*)",
            RegexOptions.Compiled);

        public static List<EnvLine> Parse(string rawText)
        {
            var lines  = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var result = new List<EnvLine>(lines.Length);

            foreach (var raw in lines)
            {
                var trimmed = raw.Trim();

                // Blank line
                if (trimmed.Length == 0)
                {
                    result.Add(new EnvLine { Raw = raw, HasSecret = false });
                    continue;
                }

                // Try active assignment first: KEY=value or export KEY=value
                var m = ActiveRegex.Match(trimmed);
                if (m.Success)
                {
                    result.Add(new EnvLine
                    {
                        Raw       = raw,
                        HasSecret = true,
                        Prefix    = m.Groups[1].Value,
                        Value     = m.Groups[2].Value
                    });
                    continue;
                }

                // Try commented assignment: # KEY=value
                m = CommentedRegex.Match(trimmed);
                if (m.Success)
                {
                    result.Add(new EnvLine
                    {
                        Raw       = raw,
                        HasSecret = true,
                        Prefix    = m.Groups[1].Value,
                        Value     = m.Groups[2].Value
                    });
                    continue;
                }

                // Pure comment or unrecognised line — show as-is
                result.Add(new EnvLine { Raw = raw, HasSecret = false });
            }

            return result;
        }
    }
}